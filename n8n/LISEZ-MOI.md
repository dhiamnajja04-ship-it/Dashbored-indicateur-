# Workflows n8n — Plateforme Indicateurs

n8n est accessible sur **http://192.168.16.138:5678**

## Importer

Dans n8n : menu **⋯** → **Import from File** → choisir un fichier de
`n8n/workflows/`.

Un seul identifiant est à créer, une fois :

| Type | Réglages |
|---|---|
| **Postgres** | hôte `postgres`, port `5432`, base `indicateurs_db`, utilisateur `postgres`, mot de passe `testpwd` |

Ollama et le Gateway sont appelés en HTTP simple, sans authentification.

> Les noms d'hôte sont ceux du réseau Docker (`postgres`, `ollama`,
> `gateway`), pas `localhost` : n8n s'exécute dans un conteneur.

---

## 1. Recherche sémantique — `1-recherche-semantique.json`

**Déclencheur** : webhook `POST /webhook/recherche`

```bash
curl -s -X POST http://localhost:5678/webhook/recherche \
  -H 'Content-Type: application/json' \
  -d '{"question":"emploi des jeunes","limite":4}'
```

**Chaîne** : question → plongement (Ollama) → `rechercher_indicateurs()` →
réponse JSON.

**Ce qu'il apporte** : une capacité que la plateforme n'a pas. La recherche
existante (`unaccent`) trouve « densite » → « Densité ». Celle-ci trouve
**« emploi » → « Taux de chômage »**, sans un seul mot commun.

Mesuré :

```
« emploi des jeunes et travail »          → IND-CHOM  0,638  (1er)
« les enfants vont-ils à l'école »        → IND-SCOL  0,730  (1er)
« accès à l'électricité dans les foyers » → IND-ELEC  0,718  (1er)
```

---

## 2. Indexation vectorielle — `2-indexation-vectorielle.json`

**Déclencheur** : toutes les 6 heures.

Interroge la vue `indicateurs_a_indexer`, qui ne remonte que les indicateurs
**nouveaux ou modifiés**. Réindexer les 12 à chaque passage serait du
gaspillage.

Sans lui, un indicateur créé après l'indexation initiale resterait invisible à
la recherche sémantique.

Équivalent en ligne de commande : `./ci/indexer-vecteurs.py`

---

## 3. Alerte des validations en attente — `3-alerte-validations.json`

**Déclencheur** : chaque jour à 8 h (fuseau `Africa/Tunis`).

Appelle `generer_alerte_validation()`, qui écrit dans `alertes_validation`
**seulement s'il y a quelque chose en attente**. Un journal rempli de « rien à
signaler » devient illisible et personne ne le consulte.

**Pourquoi une table et pas un e-mail** : aucun serveur SMTP n'est joignable
sur cette VM (ports 25 et 587 fermés). Passer par un fournisseur externe
exigerait un mot de passe d'application stocké quelque part. Une alerte en
base est consultable, versionnée, et ne dépend d'aucun secret.

Le nœud « Résumer » produit déjà le texte : brancher un e-mail ou Slack plus
tard ne demandera qu'un nœud supplémentaire, sans toucher au reste.

État réel au moment de l'écriture :

```
9 valeurs en attente · 4 en revue · 5 en brouillon
la plus ancienne attend depuis 3 jours
```

Le lien avec la règle du sujet : **tant qu'une valeur n'est pas validée, l'IA
ne la voit pas.** Une file d'attente qui grossit, c'est une analyse qui
s'appauvrit.

---

## Ce qui n'a pas été retenu

**Un workflow de surveillance de la santé** ferait doublon avec
`ci/demarrage-plateforme.sh`, lancé toutes les 10 minutes par cron — et qui
**répare** au lieu de se contenter d'alerter. Un workflow n8n serait plus
faible sur ce point, et dépendrait de n8n lui-même : si la plateforme tombe,
n8n peut tomber avec elle.

**Un rapport IA hebdomadaire** reste possible (`POST /api/ia/analyse`), mais le
modèle local fait 1,5 milliard de paramètres : un rapport automatique d'un
texte moyen a peu de valeur tant que la qualité rédactionnelle n'est pas au
niveau. Le périmètre, lui, est garanti et testé — c'est ce qui compte pour le
sujet.
