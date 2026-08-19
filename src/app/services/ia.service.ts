import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AnalyseRequest {
  question?: string;
  indicateurId?: number;
}

export interface AnalyseResponse {
  reponse: string;
  modele: string;
  nbIndicateursAnalyses: number;
  nbValeursValidees: number;
  indicateursUtilises: string[];
  genereLe: string;
}

export interface ContexteIa {
  nbIndicateurs: number;
  nbValeursValidees: number;
  prompt: string;
}

/**
 * Accès au service IA — toujours via le Gateway, jamais en direct.
 * Le service IA n'analyse que les indicateurs dont une valeur est validée
 * (règle métier de la semaine 8, appliquée côté serveur).
 */
@Injectable({ providedIn: 'root' })
export class IaService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/ia`;

  constructor(private http: HttpClient) {}

  analyser(requete: AnalyseRequest = {}): Observable<AnalyseResponse> {
    return this.http.post<AnalyseResponse>(`${this.baseUrl}/analyse`, requete);
  }

  /** Ce que l'IA voit exactement, sans appeler le modèle (utile pour la démo). */
  contexte(indicateurId?: number): Observable<ContexteIa> {
    const suffixe = indicateurId ? `?indicateurId=${indicateurId}` : '';
    return this.http.get<ContexteIa>(`${this.baseUrl}/contexte${suffixe}`);
  }
}
