/**
 * Déclencher un téléchargement depuis un contenu base64.
 *
 * Les documents (PDF, pièces jointes) transitent en base64 parce qu'ils
 * passent par une Server Action : le cookie de session est `httpOnly` et posé
 * sur l'origine du web, un lien direct vers l'API partirait sans lui.
 *
 * L'URL objet est révoquée immédiatement après le clic : la garder retiendrait
 * le fichier entier en mémoire jusqu'au rechargement de l'onglet, et une
 * planche de deux cents étiquettes pèse quelques mégaoctets.
 */
export function downloadBase64(base64: string, filename: string, mimeType: string): void {
  const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0));
  const url = URL.createObjectURL(new Blob([bytes], { type: mimeType }));

  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();

  URL.revokeObjectURL(url);
}
