import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { IaService, AnalyseResponse } from '../../services/ia.service';

/**
 * Panneau d'analyse IA (semaine 7).
 *
 * Sans `indicateurId`, l'analyse porte sur tous les indicateurs validés ;
 * avec, elle est limitée à un seul indicateur.
 *
 * Le filtrage « valeurs validées uniquement » n'est pas fait ici : il est
 * appliqué côté serveur (métier puis IA). Le composant se contente d'afficher
 * le périmètre réellement analysé, pour qu'il soit vérifiable à l'écran.
 */
@Component({
  selector: 'app-analyse-ia',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './analyse-ia.component.html',
})
export class AnalyseIaComponent {
  /** Limite l'analyse à un indicateur. Absent = synthèse globale. */
  @Input() indicateurId?: number;

  /** Titre affiché dans l'en-tête du panneau. */
  @Input() titre = 'Analyse IA';

  question = '';
  resultat: AnalyseResponse | null = null;
  chargement = false;
  erreur = '';

  constructor(private iaService: IaService) {}

  lancerAnalyse(): void {
    if (this.chargement) return;

    this.chargement = true;
    this.erreur = '';
    this.resultat = null;

    this.iaService
      .analyser({
        question: this.question.trim() || undefined,
        indicateurId: this.indicateurId,
      })
      .subscribe({
        next: (data) => {
          this.resultat = data;
          this.chargement = false;
        },
        error: (err: HttpErrorResponse) => {
          console.error("Erreur lors de l'analyse IA", err);
          this.erreur = this.messageErreur(err);
          this.chargement = false;
        },
      });
  }

  reinitialiser(): void {
    this.resultat = null;
    this.question = '';
    this.erreur = '';
  }

  /** Vrai quand le modèle a répondu mais qu'aucune donnée validée n'existait. */
  get aucuneDonneeValidee(): boolean {
    return this.resultat !== null && this.resultat.nbValeursValidees === 0;
  }

  private messageErreur(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && err.error.message) {
      return err.error.message;
    }
    if (err.status === 0) {
      return 'Impossible de contacter le Gateway. Vérifie que les services sont démarrés.';
    }
    if (err.status === 502) {
      return "Le service métier est injoignable : l'IA ne peut pas récupérer les indicateurs.";
    }
    if (err.status === 503) {
      return "Le modèle IA local est injoignable. Vérifie qu'Ollama tourne et que le modèle est téléchargé.";
    }
    if (err.status === 504) {
      return 'Le modèle a mis trop de temps à répondre. Réessaie.';
    }
    return `Une erreur est survenue (code ${err.status}).`;
  }
}
