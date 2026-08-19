import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/** Statuts du workflow de validation (miroir de MetierService/Models/StatutValeur.cs). */
export type Statut = 'Brouillon' | 'EnRevue' | 'Valide' | 'Rejete';

/**
 * Libellés affichés à l'écran.
 *
 * Ce sont uniquement des étiquettes : les valeurs techniques envoyées à l'API
 * (`Brouillon`, `EnRevue`, `Valide`, `Rejete`) sont inchangées, tout comme la
 * règle métier. Seul l'état `Valide` met `is_valid = true` et fait entrer la
 * valeur dans le périmètre analysé par l'IA.
 */
export const LIBELLES_STATUT: Record<Statut, string> = {
  Brouillon: 'Brouillon',
  EnRevue: 'En validation',
  Valide: 'Validation nationale',
  Rejete: 'Rejeté',
};

/** Page de résultats renvoyée par l'API quand on demande une pagination. */
export interface PageResultat<T> {
  elements: T[];
  page: number;
  taille: number;
  total: number;
  nbPages: number;
}

/** Agent du référentiel (table « utilisateurs »). Ce n'est pas un compte. */
export interface Utilisateur {
  id: number;
  nomUtilisateur: string;
  role?: string;
  actif: boolean;
}

/** Document justificatif attaché à un indicateur (table « meta_data »). */
export interface Document {
  id: number;
  indicateurId?: number;
  description?: string;
  nomFichier?: string;
  nomStocke?: string;
  typeMime?: string;
  tailleOctets?: number;
  deposePar?: string;
  deposeLe?: string;
}

export interface ValeurIndicateur {
  id?: number;
  indicateurId: number;
  organisationId: number;
  periodeId: number;
  valeur: number;
  /** Territoire de la mesure. Gouvernorat vide = valeur nationale. */
  pays?: string;
  gouvernorat?: string;
  statut?: string;
  degreDeFiabilite?: string;
  commentaire?: string;
  saisiePar?: string;
  saisieLe?: string;
  updateAt?: string;
  isValid?: boolean;
  validePar?: string;
}

export interface Indicateur {
  id?: number;
  code: string;
  nom: string;
  description?: string;
  statut?: string;
  unite: string;
  sourceDeDonner?: string;
  typeCollecte: string;
  frequence?: string;
  valeurCible?: number;
  anneeReference?: number;
  categorieId: number;
  valeursIndicateurs?: ValeurIndicateur[];
}

/** Réponse des endpoints de changement de statut. */
export interface ResultatStatut {
  id: number;
  statut: Statut;
  isValid: boolean;
  validePar: string;
  updateAt: string;
  transitionsPossibles: Statut[];
}

/**
 * Toutes les requêtes passent par le Gateway (jamais directement par le
 * service métier), conformément à l'architecture définie en semaine 1.
 * L'URL est relative : voir src/environments/environment.ts.
 */
@Injectable({ providedIn: 'root' })
export class IndicateurService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/indicators`;

  constructor(private http: HttpClient) {}

  // --- CRUD Indicateurs ---

  getAll(): Observable<Indicateur[]> {
    return this.http.get<Indicateur[]>(this.baseUrl);
  }

  /**
   * Page d'indicateurs, filtrée et triée **côté base**.
   *
   * Jusqu'ici la liste entière était chargée puis filtrée dans le navigateur.
   * Cette méthode déporte le travail sur PostgreSQL : seule la page demandée
   * transite. `getAll()` est conservée telle quelle pour les écrans qui ont
   * besoin de la totalité (tableau de bord, statistiques).
   */
  getPage(options: {
    page: number;
    taille: number;
    recherche?: string;
    tri?: string;
    desc?: boolean;
  }): Observable<PageResultat<Indicateur>> {
    let params = new HttpParams()
      .set('page', options.page)
      .set('taille', options.taille);

    if (options.recherche?.trim()) params = params.set('recherche', options.recherche.trim());
    if (options.tri) params = params.set('tri', options.tri);
    if (options.desc) params = params.set('desc', true);

    return this.http.get<PageResultat<Indicateur>>(this.baseUrl, { params });
  }

  // --- Référentiel des agents ---

  getUtilisateurs(role?: string): Observable<Utilisateur[]> {
    const url = `${environment.apiBaseUrl}/api/utilisateurs`;
    return role
      ? this.http.get<Utilisateur[]>(url, { params: new HttpParams().set('role', role) })
      : this.http.get<Utilisateur[]>(url);
  }

  // --- Documents justificatifs ---

  getDocuments(indicateurId: number): Observable<Document[]> {
    return this.http.get<Document[]>(`${this.baseUrl}/${indicateurId}/documents`);
  }

  /**
   * Dépose un fichier. Le corps est un FormData : on ne fixe surtout pas
   * l'en-tête Content-Type à la main, sinon la limite multipart générée par le
   * navigateur serait perdue et le serveur ne saurait pas découper le corps.
   */
  deposerDocument(
    indicateurId: number,
    fichier: File,
    description?: string,
    deposePar?: string,
  ): Observable<Document> {
    const corps = new FormData();
    corps.append('fichier', fichier, fichier.name);
    if (description) corps.append('description', description);
    if (deposePar) corps.append('deposePar', deposePar);
    return this.http.post<Document>(`${this.baseUrl}/${indicateurId}/documents`, corps);
  }

  urlTelechargement(documentId: number): string {
    return `${this.baseUrl}/documents/${documentId}`;
  }

  supprimerDocument(documentId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/documents/${documentId}`);
  }

  getById(id: number): Observable<Indicateur> {
    return this.http.get<Indicateur>(`${this.baseUrl}/${id}`);
  }

  creerIndicateur(indicateur: Indicateur): Observable<Indicateur> {
    return this.http.post<Indicateur>(this.baseUrl, indicateur);
  }

  modifierIndicateur(id: number, indicateur: Indicateur): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, indicateur);
  }

  supprimerIndicateur(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // --- CRUD Valeurs d'indicateur ---

  getValeurs(indicateurId: number, validesUniquement = false): Observable<ValeurIndicateur[]> {
    const suffixe = validesUniquement ? '?validesUniquement=true' : '';
    return this.http.get<ValeurIndicateur[]>(`${this.baseUrl}/${indicateurId}/valeurs${suffixe}`);
  }

  creerValeur(
    indicateurId: number,
    valeur: Partial<ValeurIndicateur>,
  ): Observable<ValeurIndicateur> {
    return this.http.post<ValeurIndicateur>(`${this.baseUrl}/${indicateurId}/valeurs`, valeur);
  }

  modifierValeur(valeur: ValeurIndicateur): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/values/${valeur.id}`, valeur);
  }

  supprimerValeur(valueId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/values/${valueId}`);
  }

  // --- Workflow de validation (semaine 8) ---

  /** Passe la valeur à « Validé » : elle devient visible par le service IA. */
  validerValeur(valueId: number, utilisateur = 'Administrateur'): Observable<ResultatStatut> {
    return this.http.patch<ResultatStatut>(`${this.baseUrl}/values/${valueId}/validate`, {
      utilisateur,
    });
  }

  /** Retire la valeur du périmètre de l'IA sans la supprimer. */
  devaliderValeur(valueId: number, utilisateur = 'Administrateur'): Observable<ResultatStatut> {
    return this.http.patch<ResultatStatut>(`${this.baseUrl}/values/${valueId}/devalidate`, {
      utilisateur,
    });
  }

  /** Transition libre : Brouillon → EnRevue → Valide, ou Rejete. */
  changerStatut(
    valueId: number,
    statut: Statut,
    utilisateur = 'Administrateur',
    commentaire?: string,
  ): Observable<ResultatStatut> {
    return this.http.patch<ResultatStatut>(`${this.baseUrl}/values/${valueId}/statut`, {
      statut,
      utilisateur,
      commentaire,
    });
  }
}
