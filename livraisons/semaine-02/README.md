# Semaine 2 — Frontend Angular (statique)

## Objectif

Premier conteneur sur Kubernetes : valider le design des écrans et la chaîne de
déploiement **avant** d'avoir un backend.

## Écrans livrés

| Route | Écran | Contenu |
|---|---|---|
| `/` | Tableau de bord | Compteurs (indicateurs, valeurs, validées, en attente) + cartes |
| `/indicateurs` | Liste | Tableau des indicateurs, formulaire de création/édition |
| `/indicateurs/:id` | Détail | Fiche de l'indicateur + tableau de ses valeurs |

Les écrans suivent le modèle de données de la semaine 1 : un indicateur porte un
`code`, un `nom`, une `unite`, une `frequence` ; ses valeurs sont listées à part
avec leur statut de validation.

## Données statiques

À ce stade, `IndicateurService` renvoyait des tableaux codés en dur (aucun appel
HTTP). Le contrat TypeScript (`interface Indicateur`, `interface
ValeurIndicateur` dans [`src/app/services/indicateur.service.ts`](../../src/app/services/indicateur.service.ts))
a été écrit dès cette semaine pour correspondre aux tables — ce qui a permis, en
semaine 6, de ne remplacer que le corps des méthodes sans toucher aux composants.

## Conteneurisation

[`Dockerfile`](../../Dockerfile) — build multi-étapes :

1. `node:22-alpine` → `npm ci` puis `npm run build` ;
2. `nginx:1.27-alpine` → sert `dist/dashboard-indicateurs/browser`.

`try_files $uri $uri/ /index.html` dans [`nginx.conf`](../../nginx.conf) : sans
cette ligne, rafraîchir la page sur `/indicateurs/3` renverrait un 404, car
nginx chercherait un fichier de ce nom.

```bash
eval $(minikube docker-env)
docker build -t indicateurs/frontend:1.0 .
kubectl apply -f k8s/00-namespace.yaml
kubectl apply -f k8s/50-frontend.yaml
```

## Résultat attendu

```bash
minikube ip                      # ex. 192.168.49.2
```

Interface : **`http://<IP_MINIKUBE>:30080`**

```bash
kubectl -n indicateurs get pods,svc
```

## Captures

- `capture-liste.png` — *(à ajouter)*
- `capture-detail.png` — *(à ajouter)*

## Remarque

Le service `frontend` est en `NodePort` et non en `LoadBalancer` : Minikube n'a
pas de contrôleur de LoadBalancer par défaut, un service de ce type resterait en
`<pending>` indéfiniment.
