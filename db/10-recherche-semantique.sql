-- =====================================================================
-- Recherche sémantique sur les indicateurs.
--
--   psql -h <IP> -U <USER> -d <BASE> -f db/10-recherche-semantique.sql
--
-- Script idempotent.
--
-- La logique vit ici plutôt que dans le workflow n8n : une fonction SQL est
-- testable en une requête, versionnée avec le reste, et réutilisable par
-- n'importe quel client. Un workflow graphique n'offre aucune de ces trois
-- propriétés.
-- =====================================================================

BEGIN;

-- Texte à vectoriser pour un indicateur. Concentré en un seul endroit : si la
-- composition change, il faut recalculer TOUS les vecteurs, et cette fonction
-- rend cette dépendance explicite.
CREATE OR REPLACE FUNCTION public.contenu_indexable(p_indicateur_id integer)
RETURNS text
LANGUAGE sql STABLE AS $$
    SELECT concat_ws('. ',
        i.nom,
        nullif(i.description, ''),
        nullif(i.source_de_donner, ''),
        nullif(i.type_collecte, ''),
        (SELECT c.nom FROM public.categories c WHERE c.id = i.categorie_id)
    )
    FROM public.indicateurs i
    WHERE i.id = p_indicateur_id;
$$;

-- Recherche par similarité cosinus.
--
-- L'opérateur <=> renvoie une DISTANCE (0 = identique). On la convertit en
-- score de similarité, plus parlant pour un utilisateur.
CREATE OR REPLACE FUNCTION public.rechercher_indicateurs(
    p_embedding vector(768),
    p_limite    integer DEFAULT 5
)
RETURNS TABLE (
    indicateur_id integer,
    code          character varying,
    nom           character varying,
    unite         character varying,
    similarite    numeric
)
LANGUAGE sql STABLE AS $$
    SELECT i.id,
           i.code,
           i.nom,
           i.unite,
           round((1 - (e.embedding <=> p_embedding))::numeric, 4) AS similarite
    FROM public.indicateurs_embeddings e
    JOIN public.indicateurs i ON i.id = e.indicateur_id
    WHERE e.embedding IS NOT NULL
    ORDER BY e.embedding <=> p_embedding
    LIMIT p_limite;
$$;

-- Indicateurs restant à indexer : jamais vectorisés, ou modifiés depuis.
CREATE OR REPLACE VIEW public.indicateurs_a_indexer AS
    SELECT i.id, i.code, i.nom, public.contenu_indexable(i.id) AS contenu
    FROM public.indicateurs i
    LEFT JOIN public.indicateurs_embeddings e ON e.indicateur_id = i.id
    WHERE e.indicateur_id IS NULL
       OR e.contenu IS DISTINCT FROM public.contenu_indexable(i.id);

COMMIT;

-- Contrôles.
SELECT 'indicateurs à indexer : ' || count(*) AS etat FROM public.indicateurs_a_indexer;
SELECT 'exemple de contenu : ' || contenu AS apercu FROM public.indicateurs_a_indexer LIMIT 1;
