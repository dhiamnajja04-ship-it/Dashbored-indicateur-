import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { IndicateurService, Indicateur } from '../../services/indicateur.service';

@Component({
  selector: 'app-indicateurs-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './indicateurs-list.component.html',
})
export class IndicateursListComponent implements OnInit {
  indicateurs: Indicateur[] = [];
  loading = true;
  erreur = '';

  afficherFormulaire = false;
  modeEdition = false;
  indicateurCourant: Indicateur = this.indicateurVide();

  // Empêche le double-clic et affiche une erreur lisible si l'enregistrement échoue
  // (ex: code déjà utilisé -> violation de contrainte unique côté base de données).
  enregistrementEnCours = false;
  erreurFormulaire = '';

  constructor(
    private indicateurService: IndicateurService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.loading = true;
    this.cdr.markForCheck();
    this.erreur = '';
    this.cdr.markForCheck();
    this.indicateurService.getAll().subscribe({
      next: (data) => {
        this.indicateurs = data;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Erreur lors du chargement des indicateurs', err);
        this.erreur = 'Impossible de charger les indicateurs.';
        this.cdr.markForCheck();
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private indicateurVide(): Indicateur {
    return {
      code: '',
      nom: '',
      description: '',
      unite: '',
      typeCollecte: '',
      frequence: '',
      categorieId: 1,
    };
  }

  private messageErreur(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && err.error.message) {
      return err.error.message;
    }
    if (err.status === 0) {
      return "Impossible de contacter le serveur. Vérifie que le Gateway et le service métier sont démarrés.";
    }
    return `Une erreur est survenue (code ${err.status}). Réessaie ou vérifie les données saisies.`;
  }

  ouvrirCreation(): void {
    this.modeEdition = false;
    this.indicateurCourant = this.indicateurVide();
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
    this.afficherFormulaire = true;
  }

  ouvrirEdition(ind: Indicateur): void {
    this.modeEdition = true;
    this.indicateurCourant = { ...ind };
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
    this.afficherFormulaire = true;
  }

  annuler(): void {
    this.afficherFormulaire = false;
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
  }

  enregistrer(): void {
    if (this.enregistrementEnCours) return; // évite le double-submit
    this.enregistrementEnCours = true;
    this.cdr.markForCheck();
    this.erreurFormulaire = '';
    this.cdr.markForCheck();

    if (this.modeEdition && this.indicateurCourant.id) {
      this.indicateurService.modifierIndicateur(this.indicateurCourant.id, this.indicateurCourant).subscribe({
        next: () => {
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.afficherFormulaire = false;
          this.charger();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la mise à jour', err);
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.erreurFormulaire = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    } else {
      this.indicateurService.creerIndicateur(this.indicateurCourant).subscribe({
        next: () => {
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.afficherFormulaire = false;
          this.charger();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la création', err);
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.erreurFormulaire = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    }
  }

  supprimer(ind: Indicateur): void {
    if (!ind.id) return;
    if (confirm(`Supprimer l'indicateur "${ind.nom}" ainsi que toutes ses valeurs ?`)) {
      this.indicateurService.supprimerIndicateur(ind.id).subscribe({
        next: () => this.charger(),
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la suppression', err);
          this.erreur = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    }
  }
}
