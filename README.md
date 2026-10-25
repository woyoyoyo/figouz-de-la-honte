# Figouz de la Honte 🖌️

> La pile de la honte, enfin sous contrôle.

Gestion de ma collection de figurines Warhammer : avancement de chaque figurine,
recettes de peinture et inventaire des pots.

PWA Blazor WebAssembly (.NET 10), même famille que
[RoadTrip QC](https://github.com/woyoyoyo/roadtrip-qc) et
[Mariage Planning](https://github.com/woyoyoyo/mariage-planning) —
mais **sans Gist ni base de données** : les données sont un simple fichier JSON
dans ce repo, [`data/collection.json`](data/collection.json).

## Pages

- `/` — tableau de bord : % de la collection peinte, figurines par statut, « sur la table », peintures à acheter
- `/figurines` — liste par faction, filtres faction / statut, recherche
- `/figurine/:id` — fiche : avancement en un tap (📦 → 🔧 → 🎨 → 🖌️ → ✨ → 🏆), recettes utilisées, photo, notes
- `/recettes` — recettes de peinture groupées par faction
- `/recette/:id` — étapes par zone (armure, métal, socle…) avec pastilles de couleur, copie texte, duplication
- `/peintures` — inventaire (tap ✅/⬜ pour « je l'ai »), liste de courses générée depuis les recettes
- `/parametres` — token GitHub, emplacement des données, historique

## Fonctionnement

- **Lecture** : API GitHub Contents (repo public → pas besoin de token) → copie en cache localStorage pour le hors-ligne
- **Écriture** : chaque modification est **un commit** sur `data/collection.json`
  (« Intercessors → Peinte », « Ajout recette : … »). L'historique git sert de journal et de sauvegarde.
- Avant chaque écriture, l'app relit la dernière version et envoie son SHA :
  si le fichier a changé entre-temps (autre appareil), la modification est rejouée sur la version fraîche.
- Les commits sur `data/` ne relancent pas le déploiement de l'app (`paths-ignore`).

## Token GitHub

Pour modifier depuis l'app : GitHub → Settings → Developer settings → **Fine-grained tokens** →
repo `figouz-de-la-honte` uniquement, permission **Contents : Read and write**.
Le coller dans Réglages ; il reste stocké sur l'appareil (localStorage).

## Modèle de données (`data/collection.json`)

```json
{
  "version": 1,
  "paints": [
    { "id": "p-macragge-blue-base", "name": "Macragge Blue", "brand": "Citadel",
      "type": "Base", "hex": "#0d407f", "owned": true }
  ],
  "recipes": [
    { "id": "r-…", "name": "Armure Ultramarines", "faction": "Ultramarines", "tags": ["armure"],
      "steps": [
        { "area": "Armure", "technique": "Base", "paintId": "p-macragge-blue-base", "notes": "2 couches fines" },
        { "area": "Armure", "technique": "Liseré", "paintName": "Mélange perso", "notes": "…" }
      ] }
  ],
  "miniatures": [
    { "id": "m-…", "name": "Intercessors", "game": "Warhammer 40,000", "faction": "Ultramarines",
      "quantity": 10, "status": "Painting", "recipeIds": ["r-…"], "notes": "…" }
  ]
}
```

Statuts : `Pile`, `Assembled`, `Primed`, `Painting`, `Painted`, `Based`.
Une étape référence une peinture du catalogue (`paintId`) ou un texte libre (`paintName`).

Le fichier peut aussi être modifié à la main (éditeur GitHub ou en local + commit).

## Développement

```bash
dotnet run --project WarhammerFigurines
```

## Déploiement

Push sur `main` → GitHub Actions publie sur GitHub Pages (`/figouz-de-la-honte/`).
Penser à incrémenter `AppInfo.Version`.
