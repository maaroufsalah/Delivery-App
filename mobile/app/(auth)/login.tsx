import { useRouter } from 'expo-router';
import { UserPlus } from 'lucide-react-native';
import { useEffect, useState } from 'react';
import {
  Image,
  KeyboardAvoidingView,
  Linking,
  Platform,
  Pressable,
  ScrollView,
  Text,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useI18n } from '@/i18n';
import { ApiError } from '@/lib/api';
import { type Space, spaceOf, useSession } from '@/lib/session';
import { readSlug, registerUrl } from '@/lib/tenant';
import { BRAND_LOGO, BRAND_LOGO_RATIO, IS_SINGLE_TENANT } from '@/config/brand';
import { useTenantTheme } from '@/theme/tenant-theme';
import { Button, Field, GradientHeader } from '@/ui';

/**
 * CONNEXION — M1.
 *
 * Le nom de la société s'affiche en tête : le thème est déjà chargé à ce stade
 * (`/public/company` est publique), et un livreur qui voit la marque de son
 * employeur sait qu'il a tapé le bon nom court à l'écran précédent.
 *
 * **Un compte qui n'est pas livreur est refusé ICI**, avec un message qui dit
 * quoi faire. Le laisser entrer l'amènerait sur un « Bientôt » qui ressemble à
 * une panne, alors que la vraie réponse est « utilisez le back-office ».
 *
 * **L'HABILLAGE de la maquette 1, mais pas son écran d'accueil** — L2 bloc 8,
 * décision D-L2-10. Anass dessine une page de bienvenue : logo, nom, promesse,
 * et deux boutons « Se connecter » / « Créer un compte » qui mènent AILLEURS.
 * C'est un geste de plus, chaque matin, pour quelqu'un qui se connecte tous les
 * jours. On prend donc tout ce que la maquette apporte — l'en-tête de marque,
 * le camion, la promesse, « Bienvenue 👋 » — et on garde le formulaire sur la
 * même page, avec les deux boutons côte à côte. Une page, aucun tap perdu.
 */
/**
 * Où atterrit chaque espace.
 *
 * Une table plutôt qu'un `switch` : elle se lit d'un coup d'œil, et
 * `app/index.tsx` dit la même chose pour le chemin du démarrage. Les deux
 * doivent rester d'accord — c'est la seule duplication du routage, et elle est
 * visible.
 */
const HOME_OF: Record<Space, string | null> = {
  courier: '/(courier)/home',
  merchant: '/(merchant)/home',
  staff: '/(staff)/home',
  none: null,
};

export default function LoginScreen() {
  const { t } = useI18n();
  const router = useRouter();
  const { signIn } = useSession();
  const { brand } = useTenantTheme();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [slug, setSlug] = useState<string | null>(null);

  useEffect(() => {
    void readSlug().then(setSlug);
  }, []);

  const submit = async () => {
    setBusy(true);
    setError(null);

    try {
      const profile = await signIn(email.trim(), password);

      /*
       * Les trois espaces sont ouverts — M2/M3.
       *
       * M1 ne laissait entrer que le livreur, et disait aux autres que
       * l'application n'était pas pour eux. Chacun a maintenant le sien, et
       * c'est le RATTACHEMENT qui tranche (D-072), pas un choix de l'écran.
       *
       * Un compte sans espace reste refusé plutôt qu'envoyé sur une coquille
       * vide : un administrateur de plateforme, ou un compte étiqueté sans
       * fiche, n'a rien à faire sur ce téléphone, et le lui dire vaut mieux que
       * de l'y laisser tourner en rond.
       */
      const destination = HOME_OF[spaceOf(profile)];

      if (!destination) {
        setError(t('login.noSpace'));
        return;
      }

      router.replace(destination);
    } catch (cause) {
      /*
       * Un seul message pour « mauvais mot de passe » et « compte inconnu » :
       * les distinguer dirait à qui essaie quelles adresses existent.
       *
       * UNE exception, et c'est l'API qui la décide, pas l'écran — C13 bloc 1.
       * Un compte inscrit sur le web et pas encore validé reçoit `reason:
       * 'pending-validation'`, que `api.ts` décode déjà pour tout le monde
       * (« l'écran traduit, il ne devine pas »). Lui répondre « identifiants
       * incorrects » le ferait retaper indéfiniment un mot de passe juste.
       *
       * Rien d'autre ne change côté mobile : même endpoint, même flux, deux
       * textes de plus.
       */
      const pending = cause instanceof ApiError && cause.reason === 'pending-validation';
      // Frein par adresse (recette SpeedyTrans 3) : attendre, pas retaper.
      const throttled = cause instanceof ApiError && cause.reason === 'login-throttled';
      setError(t(pending ? 'login.pending' : throttled ? 'login.throttled' : 'login.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <SafeAreaView className="flex-1 bg-background" edges={['bottom']}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        className="flex-1"
      >
        {/*
          L'EN-TÊTE DE MARQUE, exactement celui de l'accueil.

          La maquette peint un fond bleu nuit ; ce bleu-là est la couleur
          PRIMAIRE de la maquette, donc celle du tenant (orange pour
          SpeedyTrans). `GradientHeader` la dérive déjà, et `/public/company`
          est publique — la marque est donc chargée AVANT la connexion, ce qui
          est tout l'intérêt : on voit chez qui on se connecte.
        */}
        <GradientHeader height={190} logo={false}>
          <View className="gap-1">
            {/*
              LE LOGO OFFICIEL, en grand, là où il y avait un camion et le nom
              — dernières corrections, point 5. Blanc, sur l'orange de la
              marque : jamais recoloré. Le nom reste pour un lecteur d'écran.
            */}
            <Image
              source={BRAND_LOGO}
              accessibilityLabel={brand?.name ?? t('login.title')}
              resizeMode="contain"
              style={{ height: 52, width: 52 * BRAND_LOGO_RATIO }}
            />
            <Text className="text-[15px] text-white/80">{t('login.tagline')}</Text>
          </View>
        </GradientHeader>

        <ScrollView
          keyboardShouldPersistTaps="handled"
          contentContainerClassName="gap-6 px-6 pb-8 pt-6"
        >
          <View className="gap-1">
            <Text className="text-[22px] font-bold text-foreground">{t('login.welcome')}</Text>
            <Text className="text-[15px] text-muted-foreground">{t('login.welcomeHint')}</Text>
          </View>

          {/*
            « E-mail ou nom d'utilisateur » — recette SpeedyTrans 3, point 5.
            Clavier TEXTE : le clavier e-mail met l'arobase en avant et gêne la
            saisie d'un nom. L'API tranche à la forme.
          */}
          <Field
            label={t('login.identifier')}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="username"
            inputMode="text"
          />

          <Field
            label={t('login.password')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoComplete="current-password"
            onSubmitEditing={() => void submit()}
          />

          {error ? <Text className="text-[15px] text-destructive">{error}</Text> : null}

          <Button label={t('login.submit')} onPress={() => void submit()} busy={busy} full />

          {/*
            « CRÉER UN COMPTE » — la seconde porte de la maquette.

            L'inscription publique vit sur le WEB (C13) : la reproduire dans
            l'application aurait fait un second formulaire de création de compte
            à tenir d'accord avec le premier, pour un geste qu'un livreur fait
            UNE fois dans sa vie. Le bouton ouvre donc la page du tenant dans le
            navigateur, et la validation par l'agence suit son cours habituel.
          */}
          <Button
            label={t('login.register')}
            icon={UserPlus}
            tone="ghost"
            onPress={() => {
              if (slug) void Linking.openURL(registerUrl(slug));
            }}
            disabled={!slug}
            full
          />

          {/* Visible, pas caché — C12 bloc 2b. C'est le geste de quelqu'un qui
              vient d'échouer à se connecter ; le chercher serait une épreuve de
              plus, debout dans une agence à sept heures du matin. */}
          <Pressable
            accessibilityRole="button"
            className="min-h-11 items-center justify-center"
            onPress={() => router.push('/(auth)/forgot')}
          >
            <Text className="text-[15px] text-muted-foreground">{t('forgot.link')}</Text>
          </Pressable>

          {/*
            L'ENVIRONNEMENT — chantier UAT, D-199. « RECETTE » ou « UAT », lu
            sur `/public/company` comme le reste de la marque ; rien en
            production. Discret : une pastille grise sous les liens, pour qu'un
            testeur sache sur quelle pile il saisit sans que cela ressemble à
            une alerte.
          */}
          {brand?.environmentLabel ? (
            <View className="items-center">
              <Text className="rounded-md bg-muted px-2 py-0.5 text-[11px] font-semibold tracking-widest text-muted-foreground">
                {brand.environmentLabel}
              </Text>
            </View>
          ) : null}

          {/*
            LA MENTION DE SOCIÉTÉ DISPARAÎT EN MONO-CLIENT — C15 bloc 4.

            « Changer de société · speedy » n'avait de sens que tant qu'il y
            avait un choix. Sur un binaire qui n'en sert qu'un, c'est une porte
            vers un écran qui ne peut rien changer — et un livreur qui la pousse
            par curiosité se retrouve devant un formulaire qu'il ne sait pas
            remplir.

            Se tromper de société reste possible dans la variante multi-client,
            et la sortie y reste visible, pas cachée.
          */}
          {IS_SINGLE_TENANT ? null : (
            <Pressable
              accessibilityRole="button"
              className="min-h-11 items-center justify-center"
              onPress={() => router.replace('/(auth)/company')}
            >
              <Text className="text-[15px] text-muted-foreground">
                {t('tenant.change')}
                {slug ? ` · ${slug}` : ''}
              </Text>
            </Pressable>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
