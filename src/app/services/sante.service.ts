import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SantePlateforme {
  status: 'OK' | 'DEGRADED';
  gateway: string;
  metier: string;
  ia: string;
  timestamp: string;
}

/** Sonde de santé agrégée exposée par le Gateway (`GET /health/plateforme`). */
@Injectable({ providedIn: 'root' })
export class SanteService {
  constructor(private http: HttpClient) {}

  etat(): Observable<SantePlateforme> {
    return this.http.get<SantePlateforme>(`${environment.apiBaseUrl}/health/plateforme`);
  }
}
