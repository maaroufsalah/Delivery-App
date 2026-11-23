import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Haptics from 'expo-haptics';
import { useCallback, useEffect, useState } from 'react';
import { Vibration } from 'react-native';

/**
 * LE RETOUR D'UN SCAN — chantier « Retours recette 26/09 », point 3.
 *
 * « Son court "succès" quand le colis est accepté, son distinct "échec" quand
 * il est refusé (inconnu, déjà scanné, statut non éligible), plus vibration
 * légère. » Un livreur ne regarde pas son écran en chargeant la moto : il
 * ENTEND et il SENT, et il ne s'arrête que sur l'échec.
 *
 * **Un seul point d'entrée**, `scanFeedback()`, appelé par les deux scanners
 * (circuit et bon de ramassage) là où chacun posait sa vibration. Quatre
 * issues, parce que le terrain en a quatre :
 *
 * | issue      | vibration | son     | quand                                     |
 * |------------|-----------|---------|-------------------------------------------|
 * | `accepted` | succès    | succès  | le colis rejoint le circuit / le bon       |
 * | `queued`   | attention | succès  | hors ligne : pris, en file, part au retour |
 * | `duplicate`| attention | échec   | déjà scanné : on repose le colis           |
 * | `refused`  | erreur    | échec   | inconnu, autre circuit, statut, ville…     |
 *
 * **Le son est un module NATIF**, lié depuis l'APK 1.2.0 (chantier « APK
 * 1.2.0 »). Le module se charge à la demande et DANS un `try` : sur un binaire
 * qui ne le porterait pas (l'APK 1.1.0 encore installé quelque part), le
 * chargement échoue, le son est simplement absent, et la vibration reste — le
 * scanner ne plante pas.
 *
 * **Le réglage « Sons au scan »** vit en `AsyncStorage` comme la langue, le
 * thème et le verrou biométrique, et se lit UNE fois en cache mémoire : le
 * scanner l'interroge à chaque lecture, il ne peut pas attendre une promesse.
 */
export type ScanOutcome = 'accepted' | 'queued' | 'duplicate' | 'refused';

const SOUND_KEY = 'app.scanSounds';

/** Activé par défaut : la mission le dit, et un livreur ne le découvrirait pas seul. */
let soundsEnabled = true;
let preferenceLoaded = false;

/** Relit le réglage. Idempotent : la première lecture suffit, les autres ne coûtent rien. */
export async function loadScanSoundPreference(): Promise<boolean> {
  if (preferenceLoaded) return soundsEnabled;
  try {
    const saved = await AsyncStorage.getItem(SOUND_KEY);
    soundsEnabled = saved === null ? true : saved === 'true';
  } catch {
    soundsEnabled = true;
  }
  preferenceLoaded = true;
  return soundsEnabled;
}

export async function setScanSoundsEnabled(next: boolean): Promise<void> {
  soundsEnabled = next;
  preferenceLoaded = true;
  await AsyncStorage.setItem(SOUND_KEY, String(next));
}

/** L'interrupteur des Paramètres : la valeur, et de quoi la changer. */
export function useScanSounds(): {
  enabled: boolean;
  setEnabled: (next: boolean) => Promise<void>;
} {
  const [enabled, setState] = useState(soundsEnabled);

  useEffect(() => {
    void loadScanSoundPreference().then(setState);
  }, []);

  const setEnabled = useCallback(async (next: boolean) => {
    setState(next);
    await setScanSoundsEnabled(next);
  }, []);

  return { enabled, setEnabled };
}

/* --- Le son ---------------------------------------------------------------- */

/** Ce qu'on demande à un lecteur : rejouer depuis le début. */
interface Player {
  play(): void;
  seekTo(seconds: number): Promise<void>;
}

interface Players {
  success: Player;
  error: Player;
}

/**
 * `undefined` : pas encore tenté. `null` : tenté, le module natif manque (APK
 * 1.1.0) — on ne réessaie pas à chaque scan.
 */
let players: Players | null | undefined;

/**
 * LE MODE AUDIO, posé UNE fois et ATTENDU — chantier « Scanner », point 1.
 *
 * Le bip du 03/10 (carte verte, téléphone en silencieux) ne sortait pas. Trois
 * causes, lues dans `expo-audio` 57 côté Android :
 *
 *  1. les lecteurs naissaient au PREMIER scan : le premier bip partait sur un
 *     lecteur encore en préparation, en retard ou perdu ;
 *  2. le mode était posé sans être attendu, et SANS `interruptionMode` : chaque
 *     `play()` demandait alors le focus audio (`AUDIOFOCUS_GAIN_TRANSIENT`), et
 *     une perte de focus — un appel, une autre appli, la caméra d'un modèle
 *     d'entrée de gamme — mettait les lecteurs en pause, pour les rejouer plus
 *     tard, hors de propos ;
 *  3. la vibration passait par `expo-haptics`, que les modes silencieux et le
 *     réglage « retour tactile » d'Android coupent souvent.
 *
 * Désormais : `playsInSilentMode` (Android : le mode « sonnerie » n'éteint pas
 * le bip), `mixWithOthers` (aucun focus demandé — c'est le réglage que la doc
 * recommande pour les sons d'interface), lecteurs préparés dès l'ouverture du
 * scanner (`primeScanFeedback`), vibration par l'API `Vibration` du cœur.
 * Le bip suit le volume MÉDIA du téléphone : à zéro, il reste muet — c'est le
 * seul réglage que l'application ne peut pas forcer.
 */
let modeReady: Promise<void> | undefined;

function loadPlayers(): Players | null {
  if (players !== undefined) return players;

  try {
    /*
     * `require` et non `import` : un `import` statique évaluerait le module au
     * lancement, et `expo-audio` lève dès l'évaluation quand son module natif
     * n'est pas lié. Ici l'échec est attrapé, une fois, et le son est absent.
     */
    // eslint-disable-next-line @typescript-eslint/no-require-imports -- chargement DIFFERE, voulu
    const audio = require('expo-audio') as {
      createAudioPlayer: (source: number) => Player;
      setAudioModeAsync?: (mode: {
        playsInSilentMode: boolean;
        interruptionMode: 'mixWithOthers';
        shouldPlayInBackground: boolean;
        shouldRouteThroughEarpiece: boolean;
      }) => Promise<void>;
    };

    modeReady = (
      audio.setAudioModeAsync?.({
        playsInSilentMode: true,
        interruptionMode: 'mixWithOthers',
        shouldPlayInBackground: false,
        shouldRouteThroughEarpiece: false,
      }) ?? Promise.resolve()
    ).catch(() => undefined);

    players = {
      success: audio.createAudioPlayer(require('../../assets/sounds/scan-success.wav') as number),
      error: audio.createAudioPlayer(require('../../assets/sounds/scan-error.wav') as number),
    };
  } catch {
    players = null;
  }

  return players;
}

/**
 * Prépare le son AVANT le premier scan : le réglage, le mode audio, les deux
 * lecteurs. Appelé à l'ouverture d'un écran qui scanne ; idempotent.
 */
export function primeScanFeedback(): void {
  void loadScanSoundPreference();
  try {
    loadPlayers();
  } catch {
    // Pas de son : la vibration suffira.
  }
}

function playSound(kind: 'success' | 'error'): void {
  if (!soundsEnabled) return;
  const loaded = loadPlayers();
  if (!loaded) return;

  const player = loaded[kind];
  // Le mode d'abord (une seule fois, déjà résolu ensuite), puis rejouer depuis
  // le début : un lecteur arrivé au bout ne repart pas seul.
  void (modeReady ?? Promise.resolve())
    .then(() => player.seekTo(0))
    .then(() => player.play())
    .catch(() => {
      // Un `seekTo` refusé (lecteur pas encore prêt) ne doit pas taire le bip.
      try {
        player.play();
      } catch {
        // Rien de plus à tenter.
      }
    });
}

/* --- Le geste --------------------------------------------------------------- */

const HAPTIC: Record<ScanOutcome, Haptics.NotificationFeedbackType> = {
  accepted: Haptics.NotificationFeedbackType.Success,
  queued: Haptics.NotificationFeedbackType.Warning,
  duplicate: Haptics.NotificationFeedbackType.Warning,
  refused: Haptics.NotificationFeedbackType.Error,
};

const SOUND: Record<ScanOutcome, 'success' | 'error'> = {
  accepted: 'success',
  queued: 'success',
  duplicate: 'error',
  refused: 'error',
};

/**
 * La vibration COURTE, par le cœur de React Native : elle passe en mode
 * silencieux et vibreur, là où le retour haptique est souvent coupé. Un coup
 * pour « pris », deux pour « attention », trois pour « refusé ».
 */
const VIBRATION: Record<ScanOutcome, number[]> = {
  accepted: [0, 60],
  queued: [0, 60, 80, 60],
  duplicate: [0, 60, 80, 60],
  refused: [0, 90, 70, 90, 70, 90],
};

/** Vibration + son pour l'issue d'un scan. Ne lève jamais : un retour qui plante n'est pas un retour. */
export function scanFeedback(outcome: ScanOutcome): void {
  try {
    Vibration.vibrate(VIBRATION[outcome]);
  } catch {
    void Haptics.notificationAsync(HAPTIC[outcome]).catch(() => undefined);
  }
  try {
    playSound(SOUND[outcome]);
  } catch {
    // Le son est un confort ; la vibration a déjà dit l'essentiel.
  }
}
