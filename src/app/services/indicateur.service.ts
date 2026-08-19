import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
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
