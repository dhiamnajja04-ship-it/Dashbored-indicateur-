# Semaine 7 — IA dans l'interface

## Composant ajouté

[`AnalyseIaComponent`](../../src/app/components/analyse-ia/analyse-ia.component.ts)
— panneau réutilisable, placé à deux endroits :

| Emplacement | Portée |
|---|---|
| Tableau de bord (`/`) | Synthèse de **tous** les indicateurs validés |
| Détail (`/indicateurs/:id`) | Analyse **d'un seul** indicateur (`[indicateurId]`) |

Le même composant sert les deux cas : seule l'entrée `indicateurId` change, et
elle est simplement transmise au service IA.

## Déclenchement

L'analyse est lancée par un **bouton**, jamais au chargement de la page. La
génération sur CPU prend plusieurs dizaines de secondes : la déclencher
automatiquement rendrait le tableau de bord lent à chaque visite, pour une
analyse dont l'utilisateur n'a pas forcément besoin.

Un champ facultatif permet de poser une question libre ; vide, il produit une
synthèse générale.

## Pendant et après la génération

- Bouton désactivé + spinner + message expliquant que le modèle tourne en local
  (sans cela, l'attente ressemble à un blocage).
- Réponse affichée en `white-space: pre-wrap`, pour préserver les retours à la
  ligne et les listes produits par le modèle.
- Sous la réponse, des badges indiquent : le modèle utilisé, le nombre de valeurs
  validées analysées, le nombre d'indicateurs, et **le code de chaque indicateur
  transmis**. Le périmètre est donc lisible à l'écran, pas seulement dans les
  logs.

## Erreurs traduites côté interface

Chaque code HTTP devient une phrase actionnable plutôt qu'un numéro :

| Code | Message affiché |
|---|---|
| `0` | « Impossible de contacter le Gateway. Vérifie que les services sont démarrés. » |
| `502` | « Le service métier est injoignable : l'IA ne peut pas récupérer les indicateurs. » |
| `503` | « Le modèle IA local est injoignable. Vérifie qu'Ollama tourne… » |
| `504` | « Le modèle a mis trop de temps à répondre. Réessaie. » |

## Corrections apportées cette semaine

1. **Timeouts alignés de bout en bout** — nginx 300 s, Gateway 200 s, service IA
   180 s. Auparavant, nginx coupait à 60 s (valeur par défaut) et le front
   affichait une erreur alors que le modèle répondait encore.
2. **Sonde `/health/plateforme`** sur le Gateway — un seul appel indique quel
   maillon est en panne, au lieu d'inspecter chaque pod.
3. **`/health/ready` sur le métier et l'IA** — les readinessProbes empêchent
   Kubernetes de router du trafic vers un pod qui ne peut pas encore servir.
4. **Logs de contexte** — le métier journalise le nombre d'indicateurs et de
   valeurs renvoyés à l'IA à chaque appel.
5. **Documentation de déploiement** — [`k8s/README.md`](../../k8s/README.md) avec
   un tableau de dépannage.

## Capture

- `capture-ia.png` — indicateurs + réponse IA visibles à l'écran — *(à ajouter)*
