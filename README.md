# Figouz de la Honte 🖌️

> La pile de la honte, enfin sous contrôle.

Gestion de ma collection de figurines Warhammer : avancement de chaque figurine
et recettes de peinture.

PWA Blazor WebAssembly (.NET 10), même famille que
[RoadTrip QC](https://github.com/woyoyoyo/roadtrip-qc) et
[Mariage Planning](https://github.com/woyoyoyo/mariage-planning) —
mais **sans base de données** : juste des dossiers de fichiers JSON dans ce repo.

```
recettes/
  archers-sylvains.json      ← une recette = un fichier
figurines/
  archers-sylvains.json      ← une figurine / unité = un fichier
```

Le nom du fichier sert d'identifiant. **Le site est en lecture seule** : pas de compte,
pas de token. Les fichiers se modifient à la main (éditeur GitHub, ou en local + commit) ;
le site les relit directement dans le repo, sans redéploiement.

## Pages

- `/` — tableau de bord : % de la collection peinte, figurines par statut, « sur la table »
- `/figurines` — liste par faction, filtres faction / statut, recherche
- `/figurine/:fichier` — fiche : avancement en un tap (📦 → 🔧 → 🎨 → 🖌️ → ✨ → 🏆), recettes utilisées, photo, notes
- `/recettes` — recettes de peinture groupées par faction
- `/recette/:fichier` — étapes par zone (capes, peau, socle…), copie texte, duplication
- `/peintures` — toutes les peintures citées dans les recettes, par marque (calculé, rien à saisir)
- `/infos` — recharger, liens vers les fichiers et l'historique GitHub

## Formats

**Recette** (`recettes/archers-sylvains.json`) :

```json
{
  "name": "Archers Sylvains",
  "faction": "Elfes Sylvains",
  "tags": ["aérographe"],
  "steps": [
    { "technique": "Sous-couche", "paint": "Noir" },
    { "area": "Capes", "technique": "Base", "paint": "AK Amethyst Blue", "notes": "À l'aéro" }
  ]
}
```

La peinture est un texte libre : mettre la marque devant (`AK …`, `Vallejo …`, `Citadel …`)
pour qu'elle soit bien rangée dans la page Peintures. `area` vide = toute la figurine.

**Figurine** (`figurines/archers-sylvains.json`) :

```json
{
  "name": "Archers sylvains",
  "game": "The Old World",
  "faction": "Elfes Sylvains",
  "quantity": 10,
  "status": "Painting",
  "recipes": ["archers-sylvains"],
  "notes": "…"
}
```

Statuts : `Pile`, `Assembled`, `Primed`, `Painting`, `Painted`, `Based`.
`recipes` = noms des fichiers de recettes, sans `.json`.

## Fonctionnement

- L'API GitHub donne la liste des fichiers (2 appels, sans token : repo public), le contenu est lu sur
  raw.githubusercontent.com au SHA du dernier commit (pas de cache périmé, pas de quota).
- Copie en cache localStorage pour le hors-ligne ; un fichier n'est retéléchargé que s'il a changé.
- Un fichier JSON invalide est ignoré et signalé en haut de page.
- Les commits sur `recettes/` et `figurines/` ne relancent pas le déploiement de l'app (`paths-ignore`).

## Développement

```bash
dotnet run --project WarhammerFigurines
```

## Déploiement

Push sur `main` → GitHub Actions publie sur GitHub Pages (`/figouz-de-la-honte/`).
Penser à incrémenter `AppInfo.Version`.
