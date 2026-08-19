import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { IndicateurService, Indicateur } from '../../services/indicateur.service';
import { NotificationService } from '../../services/notification.service';
import { UNITES_COURANTES, TYPES_COLLECTE, FREQUENCES } from '../../reference/referentiels';

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

  /** Suggestions proposées dans le formulaire (voir reference/referentiels.ts). */
  readonly unites = UNITES_COURANTES;
  readonly typesCollecte = TYPES_COLLECTE;
  readonly frequences = FREQUENCES;

  constructor(
    private indicateurService: IndicateurService,
    private cdr: ChangeDetectorRef,
    private notifications: NotificationService,
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
      sourceDeDonner: '',
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
          this.notifications.succes(
            'Indicateur modifié',
            `« ${this.indicateurCourant.nom} » a été mis à jour.`,
          );
          this.charger();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la mise à jour', err);
          this.notifications.erreur('Modification impossible', this.messageErreur(err));
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
          this.notifications.succes(
            'Indicateur créé',
            `« ${this.indicateurCourant.nom} » a été ajouté.`,
          );
          this.charger();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la création', err);
          this.notifications.erreur('Création impossible', this.messageErreur(err));
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.erreurFormulaire = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    }
  }

  /** Date affichée dans l'en-tête de la version imprimée. */
  readonly imprimeLe = new Date();

  /**
   * Lance l'impression du tableau. La mise en page papier est entièrement
   * gérée par les règles `@media print` de styles.css : navigation, boutons
   * et panneau IA sont masqués, seul le tableau est conservé.
   */
  imprimer(): void {
    window.print();
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
