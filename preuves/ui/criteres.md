# Critères · Refonte de l'habillage du frontend ECommerce

Page : https://localhost:7218/
Pages concernées : `/` (Home), `/products` (Catalog), `/orders` (Orders), menu latéral et barre du haut.
Captures : preuves/ui/captures/

Périmètre (fichiers modifiables) :
- `src/ECommerce.Web/wwwroot/app.css`
- `src/ECommerce.Web/Components/Layout/MainLayout.razor`, `MainLayout.razor.css`
- `src/ECommerce.Web/Components/Layout/NavMenu.razor`, `NavMenu.razor.css`
- `src/ECommerce.Web/Components/Pages/Home.razor`, `Products.razor`, `Orders.razor` et leurs `.razor.css` (à créer si besoin)
- `src/ECommerce.Web/README.md`

Hors périmètre : `App.razor`, `ReconnectModal.*`, `Error.razor`, `NotFound.razor`, `Services/**`, `Program.cs`,
`wwwroot/lib/**` (Bootstrap reste chargé tel quel), toute API. Aucun CDN, aucune police téléchargée, aucun JavaScript.

Contrainte de non-régression : ne change ni la logique C# ni les textes visibles (titres `ECommerce`, `Catalog`,
`Cart`, `Orders`, libellés des boutons `Add`, `Place order`, `Refresh`, `Open Catalog`, `View Orders`, messages
de chargement, de succès et d'erreur), seulement le balisage de présentation et le CSS. Le panier, la création de
commande et le bouton `Refresh` fonctionnent comme avant.

## Phase 1 · Habillage

Avant : rien (l'application tourne via l'AppHost).

| Id | Critère |
|---|---|
| M1 | **Jetons de design** : couleurs, rayons, ombres, espacements et typographie sont définis une seule fois en propriétés personnalisées CSS (`--…`) sur `:root` dans `app.css`, et réutilisés par les autres feuilles ; aucune couleur codée en dur hors de ces jetons (sauf `transparent`/`currentColor`). |
| M2 | **Thème sombre** : avec `prefers-color-scheme: dark`, fond, texte, cartes, tableau, menu et champs passent en sombre via la redéfinition des jetons ; le contraste texte/fond reste ≥ 4,5:1 dans les deux thèmes. |
| M3 | **Typographie** : pile de polices système (`system-ui, -apple-system, "Segoe UI", Roboto, …`), tailles de titres fluides avec `clamp()`, interlignage confortable (≈ 1,5 pour le texte). |
| M4 | **Menu latéral** : le dégradé violet par défaut du gabarit est remplacé par une surface sobre issue des jetons ; le lien actif est nettement distinct (fond ou barre d'accent) ; les icônes restent alignées. |
| M5 | **Mise en page** : contenu centré avec une largeur de lecture maximale ; les cartes de Home et la liste des commandes utilisent CSS Grid (`repeat(auto-fill, minmax(…))`) au lieu de colonnes fixes ; le tableau du catalogue reste lisible (en-têtes alignés, prix et stock alignés à droite). |
| M6 | **Cartes** : coins arrondis, ombre légère, bordure discrète ; au survol (souris) une élévation douce ; le badge de statut de commande a une couleur sémantique lisible dans les deux thèmes. |
| M7 | **Interaction** : transitions courtes (≤ 200 ms) sur survol et focus, désactivées sous `prefers-reduced-motion: reduce` ; `:focus-visible` donne un anneau de focus net sur liens, boutons et champs, dans les deux thèmes. |
| M8 | **Responsive** : à 320 px de large, aucun défilement horizontal de la page (le tableau du catalogue peut défiler dans son propre conteneur) ; le menu se replie comme avant ; les boutons restent atteignables. |
| M9 | **Non-régression** : aucun texte visible n'a changé ; ajout au panier, passage de commande (succès et erreur) et `Refresh` fonctionnent ; la solution compile et les tests existants passent. |
