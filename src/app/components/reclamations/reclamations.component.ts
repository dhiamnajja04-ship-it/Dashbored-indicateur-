import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ReclamationService,
  Reclamation,
  StatutReclamation,
  LIBELLES_RECLAMATION,
} from '../../services/reclamation.service';
import { IndicateurService, Indicateur } from '../../services/indicateur.service';
import { NotificationService } from '../../services/notification.service';
import { RoleService } from '../../services/role.service';

@Component({
  selector: 'app-reclamations',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './reclamations.component.html',
})
export class ReclamationsComponent implements OnInit {
  private readonly service = inject(ReclamationService);
  private readonly indicateurService = inject(IndicateurService);
  private readonly notifications = inject(NotificationService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly roles = inject(RoleService);

  /** Seuls validateurs et administrateurs traitent les réclamations. */
  readonly peutTraiter = this.roles.peutValider;
  readonly peutSupprimer = this.roles.peutSupprimer;

  reclamations: Reclamation[] = [];
  indicateurs: Indicateur[] = [];
  loading = true;
  erreur = '';

  filtreStatut: StatutReclamation | '' = '';

  afficherFormulaire = false;
  enregistrementEnCours = false;
  erreurFormulaire = '';
  nouvelle: Reclamation = this.reclamationVide();

  /** Réclamation dont la réponse est en cours de rédaction. */
  reponseOuverte: number | null = null;
  texteReponse = '';
  traitementEnCours: number | null = null;

  readonly libelles = LIBELLES_RECLAMATION;
  readonly imprimeLe = new Date();

  ngOnInit(): void {
    this.charger();
    this.indicateurService.getAll().subscribe({
      next: (data) => {
        this.indicateurs = data;
        this.cdr.markForCheck();
      },
      error: () => {
        // La liste des indicateurs n'est qu'une aide à la saisie : son absence
        // ne doit pas empêcher de déposer une réclamation générale.
      },
    });
  }

  charger(): void {
    this.loading = true;
    this.erreur = '';
    this.cdr.markForCheck();

    this.service.lister(this.filtreStatut || undefined).subscribe({
      next: (data) => {
        this.reclamations = data;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Erreur lors du chargement des réclamations', err);
        this.erreur = this.messageErreur(err);
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  filtrer(statut: StatutReclamation | ''): void {
    this.filtreStatut = statut;
    this.charger();
  }

  compte(statut: StatutReclamation): number {
    return this.reclamations.filter((r) => r.statut === statut).length;
  }

  // --- Dépôt d'une réclamation ---

  ouvrirFormulaire(): void {
    this.nouvelle = this.reclamationVide();
    this.erreurFormulaire = '';
    this.afficherFormulaire = true;
    this.cdr.markForCheck();
  }

  annuler(): void {
    this.afficherFormulaire = false;
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
  }

  deposer(): void {
    if (this.enregistrementEnCours) return;
    this.enregistrementEnCours = true;
    this.erreurFormulaire = '';
    this.cdr.markForCheck();

    // Le champ « indicateur » est facultatif : chaîne vide => réclamation générale.
    const charge: Reclamation = {
      ...this.nouvelle,
      indicateurId: this.nouvelle.indicateurId ? Number(this.nouvelle.indicateurId) : null,
      // Champ facultatif laissé vide : on omet plutôt que d'envoyer "".
      email: this.nouvelle.email?.trim() || undefined,
    };

    this.service.deposer(charge).subscribe({
      next: () => {
        this.enregistrementEnCours = false;
        this.afficherFormulaire = false;
        this.cdr.markForCheck();
        this.notifications.succes(
          'Réclamation enregistrée',
          'Elle apparaît en statut « Nouvelle » et sera traitée par un validateur.',
        );
        this.charger();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Erreur lors du dépôt', err);
        this.enregistrementEnCours = false;
        this.erreurFormulaire = this.messageErreur(err);
        this.cdr.markForCheck();
        this.notifications.erreur('Dépôt impossible', this.messageErreur(err));
      },
    });
  }

  // --- Traitement ---

  ouvrirReponse(rec: Reclamation): void {
    this.reponseOuverte = rec.id ?? null;
    this.texteReponse = rec.reponse ?? '';
    this.cdr.markForCheck();
  }

  fermerReponse(): void {
    this.reponseOuverte = null;
    this.texteReponse = '';
    this.cdr.markForCheck();
  }

  appliquerStatut(rec: Reclamation, statut: StatutReclamation): void {
    if (!rec.id || this.traitementEnCours !== null) return;
    this.traitementEnCours = rec.id;
    this.cdr.markForCheck();

    const reponse = this.reponseOuverte === rec.id ? this.texteReponse.trim() : undefined;

    this.service.changerStatut(rec.id, statut, reponse || undefined).subscribe({
      next: () => {
        this.traitementEnCours = null;
        this.fermerReponse();
        this.notifications.succes(
          'Réclamation mise à jour',
          `Nouveau statut : ${this.libelles[statut]}.`,
        );
        this.charger();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Erreur lors du changement de statut', err);
        this.traitementEnCours = null;
        this.cdr.markForCheck();
        this.notifications.erreur('Changement refusé', this.messageErreur(err));
      },
    });
  }

  supprimer(rec: Reclamation): void {
    if (!rec.id) return;
    if (!confirm(`Supprimer définitivement la réclamation « ${rec.objet} » ?`)) return;

    this.service.supprimer(rec.id).subscribe({
      next: () => {
        this.notifications.info('Réclamation supprimée');
        this.charger();
      },
      error: (err: HttpErrorResponse) => {
        this.notifications.erreur('Suppression impossible', this.messageErreur(err));
      },
    });
  }

  // --- Présentation ---

  nomIndicateur(id: number | null | undefined): string {
    if (!id) return 'Réclamation générale';
    const ind = this.indicateurs.find((i) => i.id === id);
    return ind ? `${ind.code} — ${ind.nom}` : `Indicateur #${id}`;
  }

  classeStatut(statut: StatutReclamation | undefined): string {
    switch (statut) {
      case 'Traitee':
        return 'statut-valide';
      case 'EnCours':
        return 'statut-revue';
      case 'Rejetee':
        return 'statut-rejete';
      default:
        return 'statut-brouillon';
    }
  }

  imprimer(): void {
    window.print();
  }

  private reclamationVide(): Reclamation {
    return {
      objet: '',
      message: '',
      soumisPar: '',
      email: '',
      indicateurId: null,
    };
  }

  private messageErreur(err: HttpErrorResponse): string {
    if (err.error && typeof err.error === 'object' && err.error.message) return err.error.message;
    if (err.status === 0) {
      return 'Impossible de contacter le serveur. Vérifie que la passerelle API est démarrée.';
    }
    return `Une erreur est survenue (code ${err.status}).`;
  }
}
