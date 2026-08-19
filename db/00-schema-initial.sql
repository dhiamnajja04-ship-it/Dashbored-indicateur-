--
-- PostgreSQL database dump
--

\restrict qvg5eEStxdWScM9oCWew3h7QoW6AP7cnZCSbrRvcglhyxw12iMeWXAxyCtDHKsi

-- Dumped from database version 18.4 (Ubuntu 18.4-0ubuntu0.26.04.1)
-- Dumped by pg_dump version 18.4 (Ubuntu 18.4-0ubuntu0.26.04.1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: niveau_administratif_enum; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.niveau_administratif_enum AS ENUM (
    'pays',
    'region',
    'gouvernorat',
    'ministere'
);


ALTER TYPE public.niveau_administratif_enum OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: categories; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.categories (
    id integer NOT NULL,
    nom character varying(100) NOT NULL
);


ALTER TABLE public.categories OWNER TO postgres;

--
-- Name: categories_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.categories_id_seq OWNER TO postgres;

--
-- Name: categories_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.categories_id_seq OWNED BY public.categories.id;


--
-- Name: indicateurs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.indicateurs (
    id integer NOT NULL,
    nom_indicateur character varying(100),
    valeur numeric(15,2),
    date_enregistrement timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    categorie_id integer
);


ALTER TABLE public.indicateurs OWNER TO postgres;

--
-- Name: indicateurs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.indicateurs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.indicateurs_id_seq OWNER TO postgres;

--
-- Name: indicateurs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.indicateurs_id_seq OWNED BY public.indicateurs.id;


--
-- Name: meta_data; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.meta_data (
    id integer NOT NULL,
    indicateur_id integer,
    description text,
    source_donnee character varying(100)
);


ALTER TABLE public.meta_data OWNER TO postgres;

--
-- Name: meta_data_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.meta_data_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.meta_data_id_seq OWNER TO postgres;

--
-- Name: meta_data_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.meta_data_id_seq OWNED BY public.meta_data.id;


--
-- Name: organisations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.organisations (
    id integer NOT NULL,
    id_parent integer,
    niveau_administratif public.niveau_administratif_enum,
    nom character varying(100)
);


ALTER TABLE public.organisations OWNER TO postgres;

--
-- Name: organisations_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.organisations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.organisations_id_seq OWNER TO postgres;

--
-- Name: organisations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.organisations_id_seq OWNED BY public.organisations.id;


--
-- Name: periodes; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.periodes (
    id integer NOT NULL,
    annee integer,
    date_debut date,
    date_fin date,
    libelle character varying(50),
    type_periode character varying(20)
);


ALTER TABLE public.periodes OWNER TO postgres;

--
-- Name: periodes_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.periodes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.periodes_id_seq OWNER TO postgres;

--
-- Name: periodes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.periodes_id_seq OWNED BY public.periodes.id;


--
-- Name: test_connexion; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.test_connexion (
    id integer NOT NULL,
    message character varying(100)
);


ALTER TABLE public.test_connexion OWNER TO postgres;

--
-- Name: test_connexion_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.test_connexion_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.test_connexion_id_seq OWNER TO postgres;

--
-- Name: test_connexion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.test_connexion_id_seq OWNED BY public.test_connexion.id;


--
-- Name: utilisateurs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.utilisateurs (
    id integer NOT NULL,
    nom_utilisateur character varying(50) NOT NULL,
    role character varying(20) DEFAULT 'analyste'::character varying
);


ALTER TABLE public.utilisateurs OWNER TO postgres;

--
-- Name: utilisateurs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.utilisateurs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.utilisateurs_id_seq OWNER TO postgres;

--
-- Name: utilisateurs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.utilisateurs_id_seq OWNED BY public.utilisateurs.id;


--
-- Name: valeurs_indicateurs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.valeurs_indicateurs (
    id integer NOT NULL,
    indicateur_id integer,
    organisation_id integer,
    periode_id integer,
    valeur numeric(15,2),
    is_valid boolean DEFAULT false,
    saisie_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    statut character varying(50)
);


ALTER TABLE public.valeurs_indicateurs OWNER TO postgres;

--
-- Name: valeurs_indicateurs_id_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

CREATE SEQUENCE public.valeurs_indicateurs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


ALTER SEQUENCE public.valeurs_indicateurs_id_seq OWNER TO postgres;

--
-- Name: valeurs_indicateurs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: postgres
--

ALTER SEQUENCE public.valeurs_indicateurs_id_seq OWNED BY public.valeurs_indicateurs.id;


--
-- Name: categories id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories ALTER COLUMN id SET DEFAULT nextval('public.categories_id_seq'::regclass);


--
-- Name: indicateurs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.indicateurs ALTER COLUMN id SET DEFAULT nextval('public.indicateurs_id_seq'::regclass);


--
-- Name: meta_data id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.meta_data ALTER COLUMN id SET DEFAULT nextval('public.meta_data_id_seq'::regclass);


--
-- Name: organisations id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.organisations ALTER COLUMN id SET DEFAULT nextval('public.organisations_id_seq'::regclass);


--
-- Name: periodes id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.periodes ALTER COLUMN id SET DEFAULT nextval('public.periodes_id_seq'::regclass);


--
-- Name: test_connexion id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.test_connexion ALTER COLUMN id SET DEFAULT nextval('public.test_connexion_id_seq'::regclass);


--
-- Name: utilisateurs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.utilisateurs ALTER COLUMN id SET DEFAULT nextval('public.utilisateurs_id_seq'::regclass);


--
-- Name: valeurs_indicateurs id; Type: DEFAULT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.valeurs_indicateurs ALTER COLUMN id SET DEFAULT nextval('public.valeurs_indicateurs_id_seq'::regclass);


--
-- Name: categories categories_nom_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_nom_key UNIQUE (nom);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (id);


--
-- Name: indicateurs indicateurs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.indicateurs
    ADD CONSTRAINT indicateurs_pkey PRIMARY KEY (id);


--
-- Name: meta_data meta_data_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.meta_data
    ADD CONSTRAINT meta_data_pkey PRIMARY KEY (id);


--
-- Name: organisations organisations_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.organisations
    ADD CONSTRAINT organisations_pkey PRIMARY KEY (id);


--
-- Name: periodes periodes_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.periodes
    ADD CONSTRAINT periodes_pkey PRIMARY KEY (id);


--
-- Name: test_connexion test_connexion_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.test_connexion
    ADD CONSTRAINT test_connexion_pkey PRIMARY KEY (id);


--
-- Name: utilisateurs utilisateurs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.utilisateurs
    ADD CONSTRAINT utilisateurs_pkey PRIMARY KEY (id);


--
-- Name: valeurs_indicateurs valeurs_indicateurs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_indicateurs_pkey PRIMARY KEY (id);


--
-- Name: indicateurs indicateurs_categorie_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.indicateurs
    ADD CONSTRAINT indicateurs_categorie_id_fkey FOREIGN KEY (categorie_id) REFERENCES public.categories(id);


--
-- Name: meta_data meta_data_indicateur_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.meta_data
    ADD CONSTRAINT meta_data_indicateur_id_fkey FOREIGN KEY (indicateur_id) REFERENCES public.indicateurs(id);


--
-- Name: organisations organisations_id_parent_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.organisations
    ADD CONSTRAINT organisations_id_parent_fkey FOREIGN KEY (id_parent) REFERENCES public.organisations(id);


--
-- Name: valeurs_indicateurs valeurs_indicateurs_indicateur_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_indicateurs_indicateur_id_fkey FOREIGN KEY (indicateur_id) REFERENCES public.indicateurs(id);


--
-- Name: valeurs_indicateurs valeurs_indicateurs_organisation_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_indicateurs_organisation_id_fkey FOREIGN KEY (organisation_id) REFERENCES public.organisations(id);


--
-- Name: valeurs_indicateurs valeurs_indicateurs_periode_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.valeurs_indicateurs
    ADD CONSTRAINT valeurs_indicateurs_periode_id_fkey FOREIGN KEY (periode_id) REFERENCES public.periodes(id);


--
-- PostgreSQL database dump complete
--

\unrestrict qvg5eEStxdWScM9oCWew3h7QoW6AP7cnZCSbrRvcglhyxw12iMeWXAxyCtDHKsi

