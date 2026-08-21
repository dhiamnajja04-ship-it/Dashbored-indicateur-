# Guide de présentation

Comment présenter la plateforme au tuteur : ce qu'il attend, dans quel ordre le
montrer, et quoi répondre aux questions difficiles.

---

## 1. Ce que le sujet demande, et où c'est traité

Le sujet fixe trois attendus pour la semaine 8. Ils se vérifient à l'écran.

| Attendu du sujet | Où le montrer |
|---|---|
| « Les indicateurs stockés en PostgreSQL, affichés à l'écran » | Tableau de bord, puis liste |
| « Un workflow de validation » | Page de détail, boutons Soumettre / Valider / Dévalider |
| « Une réponse IA générée par un modèle gratuit installé en local, **uniquement à partir des indicateurs validés** » | Panneau d'analyse IA, badges de traçabilité |

Le troisième point est **le cœur de l'évaluation**. C'est celui à démontrer en
direct, pas à décrire.

---

## 2. Déroulé de démonstration (10 minutes)

### Avant de commencer

```bash
docker compose up -d
curl -s http://localhost:5169/health/plateforme
```

Attendre `{"status":"OK",...}`. Ouvrir `http://localhost:8080`.

**Lancer une première analyse « à blanc » avant l'arrivée du tuteur** : le
premier appel charge le modèle en mémoire et prend plus de temps que les
suivants. Une démonstration ne doit pas commencer par une attente.

### Étape 1 — L'architecture (1 min)

Montrer le schéma de [`livraisons/semaine-01`](livraisons/semaine-01/README.md)
et énoncer les deux décisions structurantes :

> « Le front ne parle qu'au Gateway. Et le service IA n'a **aucune** chaîne de
> connexion PostgreSQL : il passe obligatoirement par le service métier. »

### Étape 2 — Les données viennent bien de la base (1 min)

Tableau de bord : `12 indicateurs, 13 valeurs, 2 validées, 11 en attente`.

Puis prouver que ce n'est pas codé en dur :

```bash
curl -s http://localhost:5169/api/indicators | head -c 300
```

### Étape 3 — Le workflow (2 min)

Sur *Taux de chômage* : montrer les statuts, puis **Dévalider** une valeur. La
notification confirme, le badge passe à *Brouillon*.

Insister sur la règle : **modifier le chiffre d'une valeur validée la ramène en
Brouillon.** La validation porte sur un chiffre précis, pas sur une ligne.

### Étape 4 — La règle centrale, en direct (4 min)

C'est le moment clé. **Ne pas le raconter : le faire.**

1. Lancer l'analyse → la réponse cite `IND-CHOM` et `IND-SCOL`
2. Montrer les badges sous la réponse : *2 valeurs validées analysées*, et les
   **codes exacts transmis au modèle**
3. Dévalider `IND-CHOM`
4. Relancer l'analyse → **`IND-CHOM` a disparu**, le badge affiche 1 valeur

Puis la preuve sans passer par l'interface :

```bash
curl -s http://localhost:5169/api/ia/contexte
```

> « Voilà le prompt exact envoyé au modèle. Si un indicateur n'y est pas, il ne
> peut pas être dans la réponse. »

### Étape 5 — Le reste (2 min)

Rôles, réclamations, impression, recherche, localisation par gouvernorat.

---

## 3. Les quatre niveaux de la règle centrale

Question quasi certaine : *« Qu'est-ce qui garantit que l'IA ne voit pas les
valeurs non validées ? »*

Réponse — **la règle est appliquée à quatre niveaux, du plus profond au plus
visible** :

1. **Base** — une contrainte `CHECK` lie `is_valid` et `statut`. Un `UPDATE`
   manuel incohérent est rejeté par PostgreSQL.
2. **Métier** — `GET /api/indicators/validated` filtre `Where(v => v.IsValid)`.
   `POST` force `is_valid = false` : un client ne peut pas s'auto-valider.
3. **Infrastructure** — le pod IA n'a aucune chaîne de connexion PostgreSQL.
   Contourner le métier est **matériellement impossible**, pas seulement
   déconseillé.
4. **Interface** — la réponse affiche les codes réellement transmis. Le
   périmètre est vérifiable à l'œil.

Le niveau 3 est le plus convaincant : ce n'est pas une politique, c'est une
impossibilité.

---

## 4. Questions difficiles, réponses honnêtes

**« Pourquoi ce modèle plutôt que Mistral ? »**
La VM a 4 cœurs et pas de carte graphique. Mistral 7B dépassait les 200 s du
Gateway sans terminer — mesuré, pas supposé. Le basculement ne coûte qu'une
ligne : la clé `OLLAMA_MODEL`. L'architecture est indépendante du modèle.

**« La réponse IA contient une erreur. »**
Oui, et c'est documenté. Un modèle de 0,5 milliard de paramètres invente
parfois des chiffres malgré une consigne stricte. **Ce qui est garanti, c'est le
périmètre — pas la qualité rédactionnelle.** Les données envoyées sont
vérifiables via `/api/ia/contexte`. Un modèle plus gros améliorerait la
rédaction sans changer l'architecture.

**« Les rôles protègent-ils vraiment l'API ? »**
Non, et il ne faut pas prétendre l'inverse. C'est un filtrage **d'interface**.
Un `curl` direct ignore le rôle. Le sujet ne demandait pas d'authentification ;
un vrai contrôle d'accès supposerait une table utilisateurs, des jetons, et une
vérification sur chaque endpoint.

**« Pourquoi PostgreSQL dans `docker-compose` alors que le sujet l'interdit ? »**
Le déploiement de référence est `k8s/`, où PostgreSQL est externe. Compose sert
uniquement à reproduire la chaîne complète sur une machine, et le fichier le dit
explicitement en commentaire.

**« La recherche fonctionnera-t-elle avec 10 000 indicateurs ? »**
Non. Filtrage et tri sont faits en mémoire dans le navigateur. C'est adapté à
l'échelle actuelle et documenté comme tel ; au-delà de quelques centaines, il
faut des paramètres de requête côté API et de la pagination.

**« Où sont les tests ? »**
Il n'y en a pas. Le sujet n'en demandait pas. Les vérifications ont été faites
par scénario reproductible en direct — les captures de
[`livraisons/semaine-08`](livraisons/semaine-08/README.md) montrent le passage
de 2 à 1 indicateur après dévalidation.

---

## 5. Ce qu'il ne faut pas faire

- **Ne pas improviser la démonstration de la règle centrale.** C'est le point
  noté ; il se répète avant.
- **Ne pas lancer la première analyse devant le tuteur.** Chargement du modèle.
- **Ne pas survendre les rôles** comme une sécurité. Un tuteur technique le
  testera en une commande, et la crédibilité du reste en dépend.
- **Ne pas masquer les limites.** Elles sont écrites dans les livrables :
  les annoncer soi-même vaut mieux que les voir découvertes.

---

## 6. Repères chiffrés

| | |
|---|---|
| Services conteneurisés | 5 + PostgreSQL |
| Semaines livrées | 8, dans [`livraisons/`](livraisons/) |
| Endpoints exposés | 20, tous via le Gateway |
| États du workflow | 4, avec transitions contrôlées |
| Niveaux appliquant la règle centrale | 4 |

## Démontrer Docker, Kubernetes et la répartition de charge

Une seule commande, qui **prouve** chaque point par une sortie réelle :

```bash
./ci/demonstration.sh
```

Elle déroule quatre sections.

### 1. Docker

9 conteneurs actifs, santé de bout en bout, et le rappel que **PostgreSQL
n'est pas dans le cluster** — contrainte du sujet.

### 2. Répartition de charge

Ce que l'encadrant doit retenir :

```
AVANT 30 requêtes    gateway-1  6.2 kB    gateway-2  2.89 kB
APRÈS                gateway-1  155 kB    gateway-2  133 kB
```

Les deux compteurs augmentent : la charge est **réellement** distribuée, ce
n'est pas une affirmation.

Puis le script arrête une réplique et rejoue 10 requêtes :

```
avec 1 réplique sur 2 : 10/10 requêtes servies
```

> À dire à l'encadrant : la première version échouait **4 fois sur 8**, parce
> qu'un bloc `upstream` nginx ne résout les noms qu'au démarrage et figeait une
> seule réplique. La configuration passe donc par une variable dans
> `proxy_pass`, ce qui force une résolution DNS à chaque requête.

### 3. Kubernetes

5 pods prêts, et le point d'architecture qui compte :

```
frontend         NodePort    80:30080/TCP     ← exposé
gateway          NodePort    8080:30169/TCP   ← exposé
ia-service       ClusterIP   8080/TCP         ← interne
metier-service   ClusterIP   8080/TCP         ← interne
```

Le métier et l'IA sont **injoignables de l'extérieur**. Et les endpoints du
Service `gateway` montrent la répartition **native** de Kubernetes :

```
gateway   10.244.0.4:8080,10.244.0.7:8080
```

> Aucun conteneur nginx n'est nécessaire côté Kubernetes : un Service répartit
> déjà sur ses pods. Le répartiteur nginx n'existe que parce que Docker Compose
> n'offre pas cette fonction.

### 4. La règle du sujet, dans les deux environnements

```
Docker      : 12 indicateurs en base, 2 transmis à l'IA
Kubernetes  : 12 indicateurs en base, 2 transmis à l'IA
```

Le même applicatif, deux orchestrateurs, la même règle métier respectée.

## Montrer les tests

### D'abord : les faire tourner

```bash
./ci/tests-unitaires.sh
```

```
MetierService.Tests   Passed!  Failed: 0, Passed: 20
IaService.Tests       Passed!  Failed: 0, Passed: 15
```

**Ce qu'il faut dire** : ces 35 tests ne portent pas sur de l'affichage mais
sur les deux fichiers qui décident de ce que voit le modèle IA — le workflow de
validation et la construction du prompt. Une régression à cet endroit enverrait
des valeurs non validées au modèle, ce que le sujet interdit.

Un point d'ingénierie à mentionner : les projets de test **ne référencent que
les fichiers de logique**, pas les projets entiers. Inutile de tirer EF Core,
Npgsql et ASP.NET pour tester des règles pures — les tests s'exécutent en
moins de 100 ms.

### Puis : prouver qu'ils servent à quelque chose

Des tests qui passent ne prouvent rien — un test vide passe aussi. Cette
commande casse **volontairement** une règle métier, montre que le test la
rattrape, puis restaure le code :

```bash
./ci/demonstration-tests.sh
```

Déroulé à l'écran :

```
ÉTAPE 1 — le code est sain          Passed!  20/20
ÉTAPE 2 — on casse une règle        [Valide] = { Brouillon, EnRevue, Rejete }
ÉTAPE 3 — le test rattrape          Failed!  2 échecs sur 20
             Transitions_non_prevues_sont_refusees(depuis: "Valide", vers: "Rejete") [FAIL]
             Une_valeur_validee_ne_peut_pas_etre_rejetee_directement [FAIL]
ÉTAPE 4 — on restaure               Passed!  20/20
```

**Ce qu'il faut dire** : la règle cassée autorise à rejeter une valeur déjà
validée. Ce n'est pas une contrainte technique mais une règle métier — sortir
une valeur du périmètre de l'IA (dévalider) est un acte distinct de son rejet.
Deux tests différents la rattrapent, dont un cas paramétré.

Le fichier est sauvegardé avant modification et restauré par un piège sur
`EXIT` : même une interruption au clavier laisse le dépôt propre.

## Si une question porte sur ce qui n'est pas fait

Mieux vaut l'annoncer que le laisser découvrir :

- **Pas d'authentification** — les rôles filtrent l'interface, pas l'API. Le
  sujet ne la demandait pas.
- **Modèle IA léger** (1,5 milliard de paramètres) — le périmètre est garanti
  et testé ; la qualité rédactionnelle est celle d'un modèle qui tient sur
  4 cœurs sans carte graphique.
- **Pas de données historiques** — une seule mesure par indicateur, donc pas de
  courbe d'évolution. Le prompt interdit d'ailleurs explicitement au modèle de
  parler de tendance dans ce cas, et un test le vérifie.
