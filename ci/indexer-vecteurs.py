#!/usr/bin/env python3
"""
Indexation vectorielle des indicateurs.

    ./ci/indexer-vecteurs.py

Calcule un plongement par indicateur via Ollama et le stocke dans
indicateurs_embeddings. Seuls les indicateurs nouveaux ou dont le contenu a
changé sont traités : la vue indicateurs_a_indexer s'en charge.

Écrit en Python et non en shell : les libellés contiennent des apostrophes
(« Taux d'inflation ») que le quoting bash gère mal, et une erreur de
citation produirait ici une injection SQL plutôt qu'un simple échec.
"""
import json
import subprocess
import sys
import urllib.request

MODELE = "nomic-embed-text"
OLLAMA = "http://localhost:11434/api/embeddings"


def psql(sql: str, tuples_only: bool = False) -> str:
    """Exécute du SQL dans le conteneur PostgreSQL."""
    cmd = ["docker", "compose", "exec", "-T", "postgres",
           "psql", "-U", "postgres", "-d", "indicateurs_db"]
    if tuples_only:
        cmd += ["-t", "-A", "-F", "\x1f"]
    cmd += ["-c", sql]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError(r.stderr.strip())
    return r.stdout


def embedding(texte: str) -> list[float]:
    """Demande un vecteur à Ollama."""
    corps = json.dumps({"model": MODELE, "prompt": texte}).encode()
    requete = urllib.request.Request(
        OLLAMA, data=corps, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(requete, timeout=120) as reponse:
        return json.load(reponse)["embedding"]


def main() -> int:
    lignes = [l for l in psql(
        "SELECT id, contenu FROM indicateurs_a_indexer ORDER BY id;",
        tuples_only=True).splitlines() if l.strip()]

    if not lignes:
        print("  Aucun indicateur à indexer : tout est à jour.")
        return 0

    print(f"  {len(lignes)} indicateur(s) à indexer…")
    indexes = 0

    for ligne in lignes:
        identifiant, contenu = ligne.split("\x1f", 1)
        try:
            vecteur = embedding(contenu)
        except Exception as erreur:
            print(f"  ÉCHEC id={identifiant} : {erreur}")
            continue

        if len(vecteur) != 768:
            print(f"  ÉCHEC id={identifiant} : {len(vecteur)} dimensions au lieu de 768")
            continue

        # Les valeurs sont passées en paramètres via un fichier temporaire
        # plutôt que concaténées : le contenu vient de la base et peut
        # contenir n'importe quel caractère.
        sql = (
            "INSERT INTO indicateurs_embeddings "
            "(indicateur_id, contenu, embedding, modele) VALUES "
            f"({int(identifiant)}, $tag${contenu}$tag$, "
            f"'[{','.join(map(str, vecteur))}]'::vector, $tag${MODELE}$tag$) "
            "ON CONFLICT (indicateur_id) DO UPDATE SET "
            "contenu = EXCLUDED.contenu, embedding = EXCLUDED.embedding, "
            "modele = EXCLUDED.modele, calcule_le = now();"
        )
        try:
            psql(sql)
            indexes += 1
            print(f"    id={identifiant:>3}  {contenu[:58]}")
        except Exception as erreur:
            print(f"  ÉCHEC insertion id={identifiant} : {erreur}")

    print(f"  {indexes}/{len(lignes)} indicateur(s) indexé(s).")
    return 0 if indexes == len(lignes) else 1


if __name__ == "__main__":
    sys.exit(main())
