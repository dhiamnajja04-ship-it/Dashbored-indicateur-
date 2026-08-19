# Semaine 5 — Service IA + modèle local

## Flux

```
curl ──▶ gateway ──▶ ia-service ──▶ metier-service ──▶ PostgreSQL
                          │            (HTTP interne,     (hors cluster)
                          │             valeurs validées)
                          ▼
                     ollama :11434
                     modèle mistral (local, PVC)
```

**Le service IA n'a aucun accès à PostgreSQL.** Le manifest
[`k8s/30-ia.yaml`](../../k8s/30-ia.yaml) ne lui injecte aucune chaîne de
connexion : il ne peut obtenir des indicateurs que par
`http://metier-service:8080/api/indicators/validated`.

## Endpoints (via le Gateway)

| Méthode | Route | Rôle |
|---|---|---|
| `GET` | `/health` | Le conteneur tourne |
| `GET` | `/health/ready` | Ollama répond réellement |
| `POST` | `/api/ia/analyse` | Génère une analyse |
| `GET` | `/api/ia/contexte` | Montre le prompt exact, **sans** appeler le modèle |

`/api/ia/contexte` a été ajouté pour la démonstration : il rend visible ce que le
modèle reçoit, ce qui permet de vérifier le filtrage sans dépendre de la réponse
du modèle.

## Installation du modèle

```bash
kubectl -n indicateurs exec deploy/ollama -- ollama pull mistral
kubectl -n indicateurs exec deploy/ollama -- ollama list
```

Le modèle est stocké dans un PVC de 15 Gi : le téléchargement (~4 Go) n'a lieu
qu'une fois et survit aux redémarrages du pod.

## Commandes

```bash
IP=$(minikube ip)

curl -s http://$IP:30169/health/plateforme | jq   # état global de la chaîne

# Synthèse générale
curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{}' | jq

# Question libre
curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' \
  -d '{"question":"Quels indicateurs sont en dessous de leur valeur cible ?"}' | jq

# Un seul indicateur
curl -s -X POST http://$IP:30169/api/ia/analyse \
  -H 'Content-Type: application/json' -d '{"indicateurId":1}' | jq
```

## Exemple de réponse

```json
{
  "reponse": "Deux indicateurs validés sont disponibles pour l'année 2025. Le taux de chômage s'établit à 12,4 %, soit 2,4 points au-dessus de la cible de 10 %... ",
  "modele": "mistral",
  "nbIndicateursAnalyses": 2,
  "nbValeursValidees": 2,
  "indicateursUtilises": ["IND-CHOM", "IND-SCOL"],
  "genereLe": "2026-08-17T09:12:44Z"
}
```

Les champs `nbValeursValidees` et `indicateursUtilises` ne sont pas décoratifs :
ils rendent le périmètre vérifiable. Si l'IA cite un indicateur absent de
`indicateursUtilises`, c'est une hallucination — et cela se voit immédiatement.

## Réponse ancrée dans les données

Trois mesures évitent le texte inventé :

1. **Prompt fermé** — [`PromptBuilder`](../../IaService/Services/PromptBuilder.cs)
   liste les données entre marqueurs explicites et interdit tout ajout.
2. **Température 0,2** — on veut une synthèse factuelle, pas de la créativité.
3. **Aucune donnée, aucun appel** — si zéro valeur validée, le service renvoie un
   message clair sans solliciter le modèle. Interrogé à vide, un LLM produirait
   une analyse plausible et entièrement fictive.

## Pannes gérées

| Situation | Code | Message |
|---|---|---|
| Métier injoignable | `502` | « Le service métier est injoignable. » |
| Ollama injoignable / modèle absent | `503` | « …vérifie qu'Ollama tourne et que le modèle est téléchargé. » |
| Génération trop lente | `504` | « Le modèle a mis trop de temps à répondre. » |
| Aucune valeur validée | `200` | « Aucun indicateur validé n'est disponible… » |

## Performance

Première requête plus lente (chargement du modèle en RAM). Sur CPU, compter
plusieurs dizaines de secondes par génération. Les timeouts sont alignés de bout
en bout — nginx 300 s, Gateway 200 s, service IA 180 s — pour qu'aucun maillon ne
coupe avant le modèle. Si c'est trop lent en démo : passer `OLLAMA_MODEL` à
`phi3:mini` dans le ConfigMap, sans reconstruire d'image.
