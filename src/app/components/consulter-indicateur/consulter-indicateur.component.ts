import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import {
  IndicateurService,
  Indicateur,
  ValeurIndicateur,
  Statut,
  LIBELLES_STATUT,
} from '../../services/indicateur.service';
import { AnalyseIaComponent } from '../analyse-ia/analyse-ia.component';

@Component({
  selector: 'app-consulter-indicateur',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, AnalyseIaComponent],
  templateUrl: './consulter-indicateur.component.html',
})
export class ConsulterIndicateurComponent implements OnInit {
  indicateur: Indicateur | null = null;
  valeurs: ValeurIndicateur[] = [];
  indicateurId!: number;
  erreur = '';

  afficherFormulaireValeur = false;
  modeEditionValeur = false;
  valeurCourante: Partial<ValeurIndicateur> = this.valeurVide();

  // Empêche le double-clic et affiche une erreur lisible si l'enregistrement échoue.
  enregistrementEnCours = false;
  erreurFormulaire = '';

  constructor(
    private route: ActivatedRoute,
    private indicateurService: IndicateurService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.indicateurId = +idParam;
      this.chargerDetails();
    }
  }

  chargerDetails() {
    this.indicateurService.getById(this.indicateurId).subscribe({
      next: (data) => {
        this.indicateur = data;
        this.valeurs = data.valeursIndicateurs ?? [];
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Erreur lors du chargement', err);
        this.erreur = "Impossible de charger cet indicateur. Vérifie que le Gateway et le service métier sont démarrés.";
        this.cdr.markForCheck();
      },
    });
  }

  private valeurVide(): Partial<ValeurIndicateur> {
    return {
      indicateurId: this.indicateurId,
      organisationId: 1,
      periodeId: 1,
      valeur: 0,
      degreDeFiabilite: '',
      commentaire: '',
      saisiePar: '',
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

  ouvrirCreationValeur() {
    this.modeEditionValeur = false;
    this.valeurCourante = this.valeurVide();
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
    this.afficherFormulaireValeur = true;
  }

  ouvrirEditionValeur(val: ValeurIndicateur) {
    this.modeEditionValeur = true;
    this.valeurCourante = { ...val };
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
    this.afficherFormulaireValeur = true;
  }

  annulerFormulaireValeur() {
    this.afficherFormulaireValeur = false;
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
  }

  enregistrerValeur() {
    if (this.enregistrementEnCours) return; // évite le double-submit
    this.enregistrementEnCours = true;
    this.cdr.markForCheck();
    this.erreurFormulaire = '';
    this.cdr.markForCheck();

    if (this.modeEditionValeur && this.valeurCourante.id) {
      this.indicateurService.modifierValeur(this.valeurCourante as ValeurIndicateur).subscribe({
        next: () => {
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.afficherFormulaireValeur = false;
          this.chargerDetails();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la mise à jour de la valeur', err);
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.erreurFormulaire = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    } else {
      this.indicateurService.creerValeur(this.indicateurId, this.valeurCourante).subscribe({
        next: () => {
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.afficherFormulaireValeur = false;
          this.chargerDetails();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Erreur lors de la création de la valeur', err);
          this.enregistrementEnCours = false;
          this.cdr.markForCheck();
          this.erreurFormulaire = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    }
  }

  // --- Workflow de validation (semaine 8) ---
  // Brouillon → EnRevue → Validé. Seul « Validé » rend la valeur visible par l'IA.

  /** Statut en cours de changement, pour désactiver les boutons de la ligne concernée. */
  statutEnCours: number | null = null;

  libelleStatut(val: ValeurIndicateur): string {
    if (val.isValid) return LIBELLES_STATUT.Valide;
    const statut = val.statut as Statut | undefined;
    return statut && LIBELLES_STATUT[statut] ? LIBELLES_STATUT[statut] : 'Brouillon';
  }

  classeStatut(val: ValeurIndicateur): string {
    if (val.isValid) return 'bg-success';
    switch (val.statut) {
      case 'EnRevue':
        return 'bg-info text-dark';
      case 'Rejete':
        return 'bg-danger';
      default:
        return 'bg-warning text-dark';
    }
  }

  /** Nombre de valeurs actuellement transmises à l'IA pour cet indicateur. */
  get nbValeursValidees(): number {
    return this.valeurs.filter((v) => v.isValid).length;
  }

  valider(valueId: number) {
    this.appliquerStatut(valueId, 'Valide');
  }

  devalider(valueId: number) {
    this.appliquerStatut(valueId, 'Brouillon');
  }

  soumettreEnRevue(valueId: number) {
    this.appliquerStatut(valueId, 'EnRevue');
  }

  rejeter(valueId: number) {
    this.appliquerStatut(valueId, 'Rejete');
  }

  private appliquerStatut(valueId: number, statut: Statut) {
    if (this.statutEnCours !== null) return;
    this.statutEnCours = valueId;
    this.cdr.markForCheck();
    this.erreur = '';
    this.cdr.markForCheck();

    this.indicateurService.changerStatut(valueId, statut).subscribe({
      next: () => {
        this.statutEnCours = null;
        this.cdr.markForCheck();
        this.chargerDetails();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Erreur lors du changement de statut', err);
        this.statutEnCours = null;
        this.cdr.markForCheck();
        this.erreur = this.messageErreur(err);
        this.cdr.markForCheck();
      },
    });
  }

  supprimerValeur(valueId: number) {
    if (confirm('Voulez-vous supprimer cette valeur ?')) {
      this.indicateurService.supprimerValeur(valueId).subscribe({
        next: () => this.chargerDetails(),
        error: (err: HttpErrorResponse) => {
          console.error('Erreur suppression', err);
          this.erreur = this.messageErreur(err);
          this.cdr.markForCheck();
        },
      });
    }
  }
}
