# Semaine 4 — Service métier + PostgreSQL

## Flux

```
curl ──▶ gateway :30169 ──▶ metier-service:8080 ──▶ PostgreSQL <IP>:5432
         (NodePort)          (ClusterIP)             (hors cluster)
                                   ▲
                                   └── Secret k8s « postgres-secret »
                                       ConnectionStrings__DefaultConnection
```

`metier-service` est en `ClusterIP` : aucun accès direct depuis l'extérieur,
comme demandé.

## Chaîne de connexion

Elle n'est **jamais** dans Git :

- `MetierService/appsettings.json` contient une chaîne vide ;
- en production, elle vient de la variable d'environnement
  `ConnectionStrings__DefaultConnection`, alimentée par le Secret ;
- `Program.cs` refuse de démarrer si elle est absente, avec un message explicite
  — un pod en `CrashLoopBackOff` avec la cause dans les logs est préférable à un
  service qui démarre et renvoie des 500 à chaque requête.

```bash
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"
```

## Préparation de la base

Le dump initial (`schema_db.sql`) ne correspondait pas au modèle EF Core. Deux
écarts provoquaient des erreurs 500 :

1. **Noms de colonnes** — la base exposait `saisie_at`, le code attend
   `saisie_le` ; `update_at`, `valide_par`, `commentaire`, `degre_de_fiabilite`
   et `saisie_par` étaient absents.
2. **Type des dates** — colonnes en `timestamp without time zone` alors que le
   code écrit des `DateTime.UtcNow`. Depuis Npgsql 6, cette combinaison lève
   *« Cannot write DateTime with Kind=Utc to PostgreSQL type timestamp without
   time zone »*, donc un 500 à **chaque** saisie et **chaque** validation.

```bash
psql -h <IP> -U <USER> -d <BASE> -f db/01-migration-alignement.sql
psql -h <IP> -U <USER> -d <BASE> -f db/02-donnees-demo.sql
```

Le script est idempotent : il peut être rejoué sans dommage.

## CRUD via le Gateway

```bash
IP=$(minikube ip); API=http://$IP:30169/api/indicators
```

### Créer

```bash
curl -s -X POST $API -H 'Content-Type: application/json' -d '{
  "code": "IND-TEST",
  "nom": "Indicateur de test",
  "description": "Créé en démonstration",
  "unite": "%",
  "typeCollecte": "Enquête",
  "frequence": "Annuelle",
  "valeurCible": 50,
  "anneeReference": 2025,
  "categorieId": 1
}' | jq
# 201 Created
```

### Lire

```bash
curl -s $API | jq                # tous
curl -s $API/1 | jq              # un seul, avec ses valeurs
```

### Modifier

```bash
curl -s -X PUT $API/1 -H 'Content-Type: application/json' -d '{
  "id": 1, "code": "IND-TEST", "nom": "Indicateur de test (modifié)",
  "unite": "%", "typeCollecte": "Enquête", "statut": "Actif", "categorieId": 1
}' -w '%{http_code}\n'
# 204
```

### Supprimer

```bash
curl -s -X DELETE $API/1 -w '%{http_code}\n'
# 204
```

### Valeurs d'un indicateur

```bash
curl -s -X POST $API/2/valeurs -H 'Content-Type: application/json' -d '{
  "indicateurId": 2, "organisationId": 1, "periodeId": 1,
  "valeur": 12.4, "degreDeFiabilite": "haute", "saisiePar": "stagiaire"
}' | jq

curl -s $API/2/valeurs | jq
```

Une valeur créée est toujours en `Brouillon` avec `isValid: false`, quelle que
soit la charge utile envoyée : le client ne peut pas s'auto-valider.

## Erreurs traduites

Les erreurs PostgreSQL courantes sont converties en messages lisibles plutôt
qu'en 500 avec stack trace :

| Cas | SQLSTATE | Réponse |
|---|---|---|
| Code déjà utilisé | `23505` | `409` « Un enregistrement avec le code … existe déjà. » |
| Catégorie inexistante | `23503` | `400` « Référence invalide… » |
| Champ obligatoire manquant | `23502` | `400` « Un champ obligatoire est manquant (…). » |

## Vérification en base

```bash
psql -h <IP> -U <USER> -d <BASE> -c \
  "SELECT i.code, v.valeur, v.statut, v.is_valid
   FROM valeurs_indicateurs v JOIN indicateurs i ON i.id = v.indicateur_id;"
```
