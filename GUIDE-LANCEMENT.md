# Guide de lancement

Comment démarrer la plateforme, la vérifier, et quoi faire quand ça coince.

---

## 1. Démarrage normal (Docker Compose)

C'est la voie à utiliser sur la VM de développement.

```bash
cd /home/stage/rw9980
docker compose up -d
```

Six conteneurs démarrent. PostgreSQL est prêt en premier, les autres attendent
qu'il réponde avant de se lancer.

### Vérifier que tout est en ligne

```bash
curl -s http://localhost:5169/health/plateforme
```

Réponse attendue — **un seul appel indique l'état de toute la chaîne** :

```json
{"status":"OK","gateway":"OK","metier":"OK","ia":"OK"}
```

Si `status` vaut `DEGRADED`, le champ fautif dit lequel des trois services ne
répond pas.

### Ouvrir l'interface

| Depuis | URL |
|---|---|
| La VM elle-même | http://localhost:8080 |
| Une machine du réseau | `http://<IP_DE_LA_VM>:8080` |
| Gateway (pour `curl`) | http://localhost:5169 |

L'IP de la VM se lit avec `hostname -I`. Elle est en DHCP : **elle peut changer
au redémarrage**.

---

## 2. Premier démarrage seulement

Le modèle IA doit être téléchargé une fois. Il persiste ensuite dans le volume
`ollama-data`.

```bash
docker compose exec ollama ollama pull qwen2.5:0.5b
docker compose exec ollama ollama list
```

La base est initialisée automatiquement au tout premier démarrage : dump,
migration, réclamations, localisation, puis données de démonstration.

---

## 3. Vérification complète en 6 minutes

1. **Tableau de bord** → les compteurs affichent `5 / 6 / 2 / 4`.
2. **Lancer l'analyse** → environ 20 s. Les badges sous la réponse doivent citer
   **uniquement `IND-CHOM` et `IND-SCOL`**. C'est la règle centrale du sujet.
3. **Indicateurs → Détails sur Taux de chômage → Dévalider** → une notification
   apparaît en haut à droite.
4. **Relancer l'analyse** → `IND-CHOM` a disparu de la réponse. **Revalider**
   pour restaurer l'état de démonstration.
5. **Menu des rôles** (en haut à droite) → en *Agent de saisie*, les boutons
   Valider et Supprimer disparaissent.
6. **Imprimer** (`Ctrl+P`) → l'aperçu montre l'en-tête Pictor Solution, sans la
   colonne Actions ni la barre de navigation.

Contrôle en ligne de commande du périmètre exact transmis au modèle :

```bash
curl -s http://localhost:5169/api/ia/contexte | head -c 400
```

---

## 4. Problèmes fréquents

### L'interface reste sur « Chargement… »

Le navigateur sert un ancien bundle JavaScript. **`Ctrl+Shift+R`** pour forcer le
rechargement. Ce piège a coûté du temps pendant le développement : le service
répondait correctement, seul le cache était en cause.

### « Firefox is already running, but is not responding »

Un processus Firefox fantôme bloque le profil.

```bash
firefox --no-remote --profile /tmp/ff-site --new-window http://localhost:8080/
```

### Les conteneurs sont tous arrêtés

Ils tombent ensemble quand la session de la VM se termine (`exit 255`, sans
OOM). Ce n'est pas une panne applicative.

```bash
docker compose up -d
```

### L'analyse IA renvoie 504

Le modèle a dépassé le délai. Sur 4 cœurs sans carte graphique, c'est le
symptôme d'un modèle trop lourd.

```bash
docker compose exec ollama ollama ps      # le modèle est-il chargé ?
docker compose logs --tail=40 ia-service
```

Le premier appel après un démarrage est le plus lent : il inclut le chargement
du modèle en mémoire.

### PostgreSQL injoignable

```bash
docker compose ps                          # postgres est-il "healthy" ?
docker compose logs --tail=30 postgres
```

Le service métier n'accepte pas de trafic tant que la base ne répond pas — c'est
le rôle de sa sonde `/health/ready`.

---

## 5. Commandes utiles

```bash
docker compose ps                     # état des six conteneurs
docker compose logs -f metier-service # suivre un service
docker compose restart ia-service     # redémarrer un seul service
docker compose down                   # arrêter
docker compose down -v                # arrêter ET effacer la base
```

⚠️ `down -v` supprime le volume PostgreSQL **et** le modèle IA téléchargé. Le
prochain démarrage exigera de refaire le `ollama pull`.

---

## 6. Déploiement Kubernetes

`docker compose` conteneurise PostgreSQL **par commodité de développement**. Le
déploiement de référence reste [`k8s/`](k8s/), où PostgreSQL est un serveur
externe, comme l'impose le sujet.

```bash
kubectl create namespace indicateurs
 kubectl -n indicateurs create secret generic postgres-secret \
   --from-literal=connection-string="Host=<IP>;Port=5432;Database=<BASE>;Username=<USER>;Password=<MDP>"
./k8s/deploy.sh
kubectl -n indicateurs exec deploy/ollama -- ollama pull qwen2.5:0.5b
```

L'espace avant la commande `kubectl create secret` est volontaire : il évite que
le mot de passe reste dans l'historique du shell.

Interface : `http://$(minikube ip):30080`

Détails et dépannage : [`k8s/README.md`](k8s/README.md).

---

## Vérifier que tout tourne

### En une commande

```bash
./ci/demonstration.sh
```

Elle prouve, sortie à l'appui : les conteneurs Docker, la répartition de
charge (compteurs réseau avant/après, puis panne d'une réplique), les pods
Kubernetes, et la règle du sujet dans les deux environnements.

### Les tests

```bash
./ci/tests-unitaires.sh      # 35 tests des règles métier, sans base ni réseau
./ci/verifier-plateforme.sh  # 17 contrôles sur une plateforme démarrée
```

### Vérifications ciblées

| Ce qu'on veut savoir | Commande |
|---|---|
| La chaîne complète répond | `curl -s localhost:5169/health/plateforme` |
| Les conteneurs tournent | `docker compose ps` |
| Le répartiteur est actif | `curl -s localhost:5169/lb-health` |
| Les répliques du Gateway | `docker compose ps gateway` |
| La charge est distribuée | `docker stats --no-stream \| grep gateway` |
| Les pods Kubernetes | `kubectl -n indicateurs get pods` |
| Ce que voit l'IA | `curl -s localhost:5169/api/ia/contexte` |
| Les données en base | `docker compose exec postgres psql -U postgres -d indicateurs_db -c "SELECT count(*) FROM indicateurs;"` |
