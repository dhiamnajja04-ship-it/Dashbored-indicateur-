-- =====================================================================
-- Extension vectorielle, en prévision de n8n et de la recherche sémantique.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/09-pgvector.sql
--
-- Script idempotent.
--
-- pgvector fournit le type « vector » et les opérateurs de distance
-- (cosinus, L2, produit scalaire) nécessaires à une recherche par
-- similarité — chercher « emploi des jeunes » et retrouver « taux de
-- chômage » sans que les mots correspondent.
--
-- ATTENTION : l'extension doit être PRÉSENTE dans l'image. L'image officielle
-- postgres:17 ne la contient pas ; docker-compose.yml utilise donc
-- pgvector/pgvector:pg17.
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS vector;

-- Table des plongements : un vecteur par indicateur, calculé à partir de son
-- code, de son nom et de sa description.
--
-- 768 dimensions correspondent aux modèles d'embedding courants servis par
-- Ollama (nomic-embed-text). À ajuster si un autre modèle est retenu.
CREATE TABLE IF NOT EXISTS public.indicateurs_embeddings (
    indicateur_id integer PRIMARY KEY
        REFERENCES public.indicateurs(id) ON DELETE CASCADE,
    contenu       text NOT NULL,
    embedding     vector(768),
    modele        character varying(80),
    calcule_le    timestamptz NOT NULL DEFAULT now()
);

-- Index de similarité cosinus. ivfflat exige des données pour être efficace ;
-- il est créé maintenant pour que le schéma soit complet, et se peuplera au
-- fur et à mesure.
CREATE INDEX IF NOT EXISTS indicateurs_embeddings_cosine_idx
    ON public.indicateurs_embeddings
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 10);

COMMIT;

-- Contrôles.
SELECT extname || ' version ' || extversion AS extension
FROM pg_extension WHERE extname = 'vector';

SELECT 'table indicateurs_embeddings : ' || count(*) || ' ligne(s)' AS etat
FROM public.indicateurs_embeddings;
