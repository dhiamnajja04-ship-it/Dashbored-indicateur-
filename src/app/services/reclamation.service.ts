import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/** Statuts du cycle de vie d'une réclamation (miroir de StatutReclamation.cs). */
export type StatutReclamation = 'Nouvelle' | 'EnCours' | 'Traitee' | 'Rejetee';

export const LIBELLES_RECLAMATION: Record<StatutReclamation, string> = {
  Nouvelle: 'Nouvelle',
  EnCours: 'En cours de traitement',
  Traitee: 'Traitée',
  Rejetee: 'Rejetée',
};

export interface Reclamation {
  id?: number;
  indicateurId?: number | null;
  objet: string;
  message: string;
  soumisPar: string;
  email?: string;
  statut?: StatutReclamation;
  reponse?: string | null;
  creeLe?: string;
  traiteLe?: string | null;
}

export interface StatistiquesReclamations {
  total: number;
  parStatut: { statut: StatutReclamation; nombre: number }[];
}

/**
 * Réclamations déposées sur les indicateurs.
 * Comme le reste, tout passe par le Gateway — jamais par le métier en direct.
 */
@Injectable({ providedIn: 'root' })
export class ReclamationService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/reclamations`;

  constructor(private http: HttpClient) {}

  lister(statut?: StatutReclamation, indicateurId?: number): Observable<Reclamation[]> {
    const params: string[] = [];
    if (statut) params.push(`statut=${statut}`);
    if (indicateurId) params.push(`indicateurId=${indicateurId}`);
    const suffixe = params.length ? `?${params.join('&')}` : '';
    return this.http.get<Reclamation[]>(`${this.baseUrl}${suffixe}`);
  }

  statistiques(): Observable<StatistiquesReclamations> {
    return this.http.get<StatistiquesReclamations>(`${this.baseUrl}/statistiques`);
  }

  deposer(reclamation: Reclamation): Observable<Reclamation> {
    return this.http.post<Reclamation>(this.baseUrl, reclamation);
  }

  changerStatut(id: number, statut: StatutReclamation, reponse?: string): Observable<unknown> {
    return this.http.patch(`${this.baseUrl}/${id}/statut`, { statut, reponse });
  }

  supprimer(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
