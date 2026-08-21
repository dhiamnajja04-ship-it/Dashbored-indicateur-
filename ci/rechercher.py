#!/usr/bin/env python3
"""
Recherche sémantique dans les indicateurs.

    ./ci/rechercher.py "emploi des jeunes"

Vectorise la question, puis interroge pgvector par distance cosinus. Trouve
des indicateurs dont AUCUN mot n'apparaît dans la question.
"""
import json
import subprocess
import sys
import urllib.request


def embedding(texte: str) -> list[float]:
    corps = json.dumps({"model": "nomic-embed-text", "prompt": texte}).encode()
    r = urllib.request.Request("http://localhost:11434/api/embeddings",
                               data=corps, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(r, timeout=120) as rep:
        return json.load(rep)["embedding"]


def main() -> int:
    if len(sys.argv) < 2:
        print("usage : ./ci/rechercher.py \"votre question\"")
        return 1

    question = " ".join(sys.argv[1:])
    vecteur = embedding(question)

    sql = (f"SELECT code, nom, similarite FROM rechercher_indicateurs("
           f"'[{','.join(map(str, vecteur))}]'::vector, 4);")

    r = subprocess.run(
        ["docker", "compose", "exec", "-T", "postgres", "psql",
         "-U", "postgres", "-d", "indicateurs_db", "-t", "-A", "-F", "\x1f", "-c", sql],
        capture_output=True, text=True)

    print(f'\n  « {question} »\n')
    for ligne in r.stdout.splitlines():
        if not ligne.strip():
            continue
        code, nom, score = ligne.split("\x1f")
        barre = "█" * int(float(score) * 20)
        print(f"    {float(score):.3f}  {barre:<20}  {code:<10} {nom}")
    print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
