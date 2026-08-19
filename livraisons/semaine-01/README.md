# Semaine 1 — Architecture & mise en place

> À compléter avec les valeurs réelles de ta VM là où figure `<...>`.

## 1. Schéma d'architecture

```
                         ┌──────────────────────────────────────┐
  Navigateur ───────────▶│ frontend — nginx + Angular 22        │
   :30080                │ Service K8s: frontend (NodePort)     │
                         │ relaie /api ───────────┐             │
                         └────────────────────────┼─────────────┘
                                                  ▼
                         ┌──────────────────────────────────────┐
  curl ─────────────────▶│ gateway — .NET 8                     │
   :30169                │ Service K8s: gateway (NodePort)      │
                         │  /health                             │
                         │  /api/indicators/**  ──┐             │
                         │  /api/ia/**  ──────────┼───┐         │
                         └────────────────────────┼───┼─────────┘
                                                  ▼   │
                    ┌───────────────────────────────┐ │
                    │ metier-service — .NET 8       │ │
                    │ Service K8s: ClusterIP :8080  │ │
                    │ CRUD + workflow validation    │◀┼── HTTP interne
                    └───────────────┬───────────────┘ │   (pas de SQL direct)
                                    │ Secret k8s      │
                                    ▼                 ▼
                    ┌───────────────────────────┐  ┌────────────────────────┐
                    │ PostgreSQL — HORS cluster │  │ ia-service — .NET 8    │
                    │ <IP_POSTGRES>:5432        │  │ ClusterIP :8080        │
                    └───────────────────────────┘  └───────────┬────────────┘
                                                               ▼
                                                   ┌────────────────────────┐
                                                   │ ollama — modèle local  │
                                                   │ ClusterIP :11434 + PVC │
                                                   └────────────────────────┘
```

### Réponses aux questions posées dans le sujet

**Le front passe-t-il par un gateway ?**
Oui, systématiquement. Le navigateur ne connaît qu'une seule origine : nginx sert
le SPA et relaie `/api/` vers le service K8s `gateway`. Conséquences : pas d'URL
d'API codée en dur dans le bundle JavaScript, donc **une seule image Docker
réutilisable sur n'importe quelle VM**, et aucun problème de CORS en production.

**L'IA peut-elle toucher la base directement ?**
Non. `ia-service` obtient les indicateurs par un appel HTTP interne à
`http://metier-service:8080/api/indicators/validated`. Ce choix n'est pas
seulement esthétique : il est ce qui rend la règle de la semaine 8 réellement
applicable. Le filtrage « valeurs validées uniquement » est écrit **une seule
fois**, dans le métier. Si l'IA parlait à PostgreSQL, la règle devrait être
dupliquée dans les deux services et pourrait diverger. Le manifest `30-ia.yaml`
ne contient d'ailleurs aucune variable de connexion à la base : l'accès direct
est impossible, pas seulement déconseillé.

**Qui est exposé à l'extérieur ?**
Uniquement `frontend` (NodePort 30080) et `gateway` (NodePort 30169, pour les
tests curl). `metier-service`, `ia-service` et `ollama` sont en `ClusterIP`.

## 2. Modèle de données

Quatre tables principales autour des indicateurs :

| Table | Rôle | Champs principaux |
|---|---|---|
| `categories` | Regroupement thématique | `id`, `nom` (unique) |
| `indicateurs` | **Définition** d'un indicateur | `id`, `code` (unique), `nom`, `description`, `unite`, `type_collecte`, `frequence`, `valeur_cible`, `annee_reference`, `categorie_id` → `categories` |
| `valeurs_indicateurs` | **Mesures** dans le temps | `id`, `indicateur_id` → `indicateurs`, `organisation_id`, `periode_id`, `valeur`, **`statut`**, **`is_valid`**, `valide_par`, `degre_de_fiabilite`, `saisie_par`, `commentaire`, `saisie_le`, `update_at` |
| `organisations` | Qui produit la donnée (hiérarchie) | `id`, `nom`, `niveau_administratif`, `id_parent` → `organisations` |
| `periodes` | Quand | `id`, `annee`, `libelle`, `type_periode`, `date_debut`, `date_fin` |

**Décision structurante : la définition et la mesure sont séparées.**
Un indicateur (« Taux de chômage ») existe indépendamment de ses valeurs. C'est
ce qui permet de valider *une mesure précise* — le chômage 2025 du Ministère du
Plan — et non l'indicateur en bloc. Sans cette séparation, la validation
demandée en semaine 8 n'aurait pas de granularité utilisable.

**Statut de validation** — porté par `valeurs_indicateurs` :

- `statut` : étape du workflow (`Brouillon`, `EnRevue`, `Valide`, `Rejete`) ;
- `is_valid` : booléen dérivé, **seul critère lu par le service IA**.

Les deux champs sont maintenus cohérents par une contrainte `CHECK` en base
(`valeurs_statut_coherent`), pour qu'un `UPDATE` manuel ne puisse pas rendre une
valeur visible par l'IA sans passer par la validation.

Scripts : [`db/01-migration-alignement.sql`](../../db/01-migration-alignement.sql).

## 3. Choix du modèle IA

| Modèle | Taille (quantifié) | RAM nécessaire | Licence | Verdict |
|---|---|---|---|---|
| **Mistral 7B Instruct** | ~4,1 Go (Q4) | ~6 Go | Apache 2.0 | **Retenu** |
| Llama 3.1 8B Instruct | ~4,7 Go (Q4) | ~8 Go | Llama Community (restrictions) | Écarté |
| Phi-3 Mini 3.8B | ~2,3 Go (Q4) | ~4 Go | MIT | Solution de repli |

**Mistral 7B** est retenu pour trois raisons : licence Apache 2.0 sans
restriction d'usage, bonne qualité en français (les indicateurs et le prompt
sont en français), et une empreinte mémoire compatible avec la VM.

**Phi-3 Mini** est gardé comme repli : si la génération sur CPU s'avère trop
lente en démo, le basculement ne coûte qu'une ligne — la clé `OLLAMA_MODEL` du
ConfigMap `plateforme-config`. Aucune image à reconstruire.

Llama 3.1 est écarté malgré ses bons résultats : sa licence n'est pas une licence
libre au sens strict, et le sujet demande un modèle « gratuit » sans ambiguïté.

**Installation** : image officielle `ollama/ollama` dans un pod dédié, modèle
téléchargé une fois dans un `PersistentVolumeClaim` de 15 Gi :

```bash
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b
```

Le modèle n'est volontairement pas embarqué dans une image Docker : cela
donnerait une image de plus de 4 Go à reconstruire à chaque changement de code.

### Révision du choix après mesure sur la VM

Le tableau ci-dessus raisonnait sur des ordres de grandeur. Une fois la VM
disponible (**4 cœurs**, pas de GPU), la mesure a invalidé le choix de Mistral 7B.

| Mesure sur cette VM | Mistral 7B | Qwen2.5 0.5B |
|---|---|---|
| Taille du modèle | 4,4 Go | **397 Mo** |
| Temps d'une analyse via le Gateway | **> 255 s → HTTP 504** | **16–18 s → HTTP 200** |
| Charge système pendant la génération (4 cœurs) | **8,68** — machine inutilisable | **2,18** — machine réactive |

Deux conclusions :

1. **Mistral 7B est inexploitable ici.** Il ne termine pas dans les 200 s du
   Gateway, et il sature les 4 cœurs : pendant la génération, l'interface
   graphique de la VM se fige, ce qui ressemble à un plantage.
2. **Le facteur limitant est le nombre de cœurs, pas la RAM.** Les 12 Go
   suffisaient largement à Mistral ; c'est le CPU qui manque. Un modèle
   ~14 fois plus petit résout le problème sans changer une ligne de code
   applicatif.

**Modèle retenu : `qwen2.5:0.5b`** — open source sous **licence Apache 2.0**,
exécuté **en local** dans le conteneur `ollama`, conformément au sujet. Aucune
donnée ne sort de la machine.

Contrepartie assumée : à 0,5 milliard de paramètres, la synthèse est correcte et
bien ancrée dans les données validées, mais moins fluide qu'avec un modèle plus
gros (formulations répétitives). C'est un compromis conscient : une démo qui
fonctionne en 18 s prime sur une prose élégante qui fait tomber la VM.

**Marge de progression** si le tuteur dispose d'une machine plus puissante, ou
si l'on accepte ~1 min de génération : `gemma2:2b` (~1,6 Go) offre un bien
meilleur français. Le basculement reste **une seule ligne** — la variable
`Ollama__Model` dans [`docker-compose.yml`](../../docker-compose.yml) (ou la clé
`OLLAMA_MODEL` du ConfigMap en Kubernetes), suivie d'un `ollama pull`.

Mistral 7B reste téléchargé dans le volume `ollama-data` : revenir en arrière ne
coûte qu'un changement de variable.

```bash
docker compose exec ollama ollama pull qwen2.5:0.5b   # Compose (cette VM)
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b   # K8s
```

## 4. Squelette du projet

```
rw9980/
├── src/                  # Frontend Angular 22
├── GatewayService/       # .NET 8 — point d'entrée API
├── MetierService/        # .NET 8 — CRUD + workflow de validation
├── IaService/            # .NET 8 — prompt + appel du modèle local
├── db/                   # Scripts SQL (migration, données de démo)
├── k8s/                  # Manifests Kubernetes + deploy.sh
└── livraisons/           # Comptes-rendus hebdomadaires
```

## 5. Environnement VM

| Élément | Version relevée | État |
|---|---|---|
| OS | Ubuntu 26.04 LTS (noyau 7.0.0-29-generic) | OK |
| CPU / RAM | **4 cœurs**, 12 Go | OK — dimensionnement contraignant, voir §3 |
| Disque | 38 Go libres sur 67 Go | OK |
| Git | 2.53.0 | OK |
| Docker | 29.7.2 | OK |
| Docker Compose | v5.5.0 | OK |
| Ollama | 0.32.14 (conteneur `ollama/ollama`) | OK |
| Kubernetes (Minikube) | **non installé sur cette VM** | Manifests `k8s/` écrits et validés en YAML, non déployés ici |
| Accès PostgreSQL | port 5432/15432 | Atteignable |

> **Écart assumé à documenter au tuteur.** Le déploiement de référence du sujet
> est Kubernetes, et les manifests sont fournis dans [`k8s/`](../../k8s/). Mais
> Minikube n'est pas installé sur cette VM : la plateforme tourne ici via
> [`docker-compose.yml`](../../docker-compose.yml), qui reproduit la même
> topologie (mêmes images, mêmes URLs internes, métier et IA non exposés).
> Seule différence : Compose conteneurise PostgreSQL par commodité, alors que
> le sujet impose un serveur externe — ce que respecte bien `k8s/`.

```bash
git checkout -b stagiaire/dhia
git push -u origin stagiaire/dhia
```

## Points à valider avec le tuteur

1. Le découpage en 4 conteneurs applicatifs + 1 conteneur modèle.
2. La séparation `indicateurs` / `valeurs_indicateurs`, et la validation portée
   par la **valeur** et non par l'indicateur.
3. Le workflow à 4 états (`Brouillon → EnRevue → Valide`, plus `Rejete`) —
   est-ce trop, ou faut-il conserver l'étape de revue ?
4. Le choix Mistral 7B, et Phi-3 Mini comme repli.
