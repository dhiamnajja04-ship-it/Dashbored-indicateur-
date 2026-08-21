-- =====================================================================
-- Correction de l'index vectoriel.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/11-index-vectoriel.sql
--
-- Script idempotent.
--
-- PROBLÈME CONSTATÉ : l'index ivfflat créé avec lists=10 sur 12 lignes
-- renvoyait des résultats incomplets — une recherche demandant 4 voisins
-- n'en retournait que 2, et l'indicateur le plus proche était absent.
--
-- Cause : ivfflat partitionne les vecteurs en « lists » puis n'en sonde
-- qu'une seule par défaut (probes = 1). Avec 12 lignes réparties en 10
-- partitions, chacune contient environ une ligne : la quasi-totalité des
-- voisins n'est jamais examinée.
--
-- CHOIX : HNSW plutôt qu'ivfflat.
--   - ivfflat exige un volume suffisant ET un réglage de « probes » adapté
--     au nombre de lignes, donc une reconstruction à chaque changement
--     d'échelle.
--   - HNSW ne partitionne pas : son rappel reste bon dès la première ligne
--     et ne dépend pas du volume. Il coûte un peu plus à la construction,
--     ce qui est sans conséquence sur quelques milliers d'indicateurs.
-- =====================================================================

BEGIN;

DROP INDEX IF EXISTS public.indicateurs_embeddings_cosine_idx;

CREATE INDEX IF NOT EXISTS indicateurs_embeddings_hnsw_idx
    ON public.indicateurs_embeddings
    USING hnsw (embedding vector_cosine_ops);

COMMIT;

-- Contrôle : l'index en place et le nombre de vecteurs indexés.
SELECT indexname AS index_vectoriel
FROM pg_indexes
WHERE tablename = 'indicateurs_embeddings' AND indexname LIKE '%hnsw%';

SELECT count(*) || ' vecteur(s) indexé(s)' AS etat
FROM public.indicateurs_embeddings WHERE embedding IS NOT NULL;
