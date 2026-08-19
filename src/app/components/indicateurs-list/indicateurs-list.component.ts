import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { IndicateurService, Indicateur } from '../../services/indicateur.service';
import { NotificationService } from '../../services/notification.service';
import { RoleService } from '../../services/role.service';
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

  /** Voir le commentaire équivalent dans consulter-indicateur.component.ts. */
  private readonly roles = inject(RoleService);

  readonly peutGererIndicateurs = this.roles.peutGererIndicateurs;
  readonly peutSupprimer = this.roles.peutSupprimer;

  // --- Recherche et tri (purement côté client : la liste tient en mémoire) ---

  /** Texte saisi dans la barre de recherche. */
  recherche = '';

  /** Colonne servant de clé de tri. */
  triColonne: 'code' | 'nom' | 'unite' | 'sourceDeDonner' | 'valeurs' = 'code';
  triAscendant = true;

  /** L'unité est saisie librement plutôt que choisie dans la liste. */
  uniteLibre = false;

  // --- Pagination (côté serveur) ---

  page = 1;
  taille = 10;
  total = 0;
  nbPages = 0;

  /** Tailles de page proposées. */
  readonly taillesPage = [5, 10, 25, 50];

  private minuteurRecherche?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.loading = true;
    this.erreur = '';
    this.cdr.markForCheck();

    this.indicateurService
      .getPage({
        page: this.page,
        taille: this.taille,
        recherche: this.recherche,
        tri: this.triColonne === 'sourceDeDonner' ? 'source' : this.triColonne,
        desc: !this.triAscendant,
      })
      .subscribe({
        next: (resultat) => {
          this.indicateurs = resultat.elements;
          this.total = resultat.total;
          this.nbPages = resultat.nbPages;
          // Supprimer le dernier élément d'une page peut la vider : on recule.
          if (this.page > this.nbPages && this.nbPages > 0) {
            this.page = this.nbPages;
            this.charger();
            return;
          }
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Erreur lors du chargement des indicateurs', err);
          this.erreur = 'Impossible de charger les indicateurs.';
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  /**
   * Recherche différée : sans cela chaque frappe déclencherait une requête.
   * 350 ms reste réactif tout en n'envoyant qu'un appel par mot saisi.
   */
  surRecherche(): void {
    clearTimeout(this.minuteurRecherche);
    this.minuteurRecherche = setTimeout(() => {
      this.page = 1;
      this.charger();
    }, 350);
  }

  allerPage(n: number): void {
    if (n < 1 || n > this.nbPages || n === this.page) return;
    this.page = n;
    this.charger();
  }

  changerTaille(nouvelle: number): void {
    // Une valeur hors référentiel (select forcé, requête bricolée) donnerait
    // NaN ou 0, et un compteur incohérent. On retombe sur la taille par défaut.
    const valeur = Number(nouvelle);
    this.taille = this.taillesPage.includes(valeur) ? valeur : 10;
    this.page = 1;
    this.charger();
  }

  /** Fenêtre glissante de numéros de page autour de la page courante. */
  get pagesVisibles(): number[] {
    const fenetre = 5;
    let debut = Math.max(1, this.page - Math.floor(fenetre / 2));
    const fin = Math.min(this.nbPages, debut + fenetre - 1);
    debut = Math.max(1, fin - fenetre + 1);
    return Array.from({ length: fin - debut + 1 }, (_, i) => debut + i);
  }

  get premierAffiche(): number {
    return this.total === 0 ? 0 : (this.page - 1) * this.taille + 1;
  }

  get dernierAffiche(): number {
    return Math.min(this.page * this.taille, this.total);
  }

  /** La liste affichée vient déjà filtrée et triée du serveur. */
  get indicateursAffiches(): Indicateur[] {
    return this.indicateurs;
  }

  /** Clic sur un en-tête : même colonne = on inverse le sens, sinon on change de clé. */
  trierPar(colonne: 'code' | 'nom' | 'unite' | 'sourceDeDonner' | 'valeurs'): void {
    if (this.triColonne === colonne) {
      this.triAscendant = !this.triAscendant;
    } else {
      this.triColonne = colonne;
      this.triAscendant = true;
    }
    this.page = 1;
    this.charger();
  }

  iconeTri(colonne: string): string {
    if (this.triColonne !== colonne) return 'bi-arrow-down-up opacite-faible';
    return this.triAscendant ? 'bi-sort-alpha-down' : 'bi-sort-alpha-up-alt';
  }

  effacerRecherche(): void {
    this.recherche = '';
    this.page = 1;
    this.charger();
  }

  /** Bascule entre la liste d'unités et la saisie libre. */
  basculerUniteLibre(): void {
    this.uniteLibre = !this.uniteLibre;
    if (this.uniteLibre) {
      this.indicateurCourant.unite = '';
    }
  }

  /** Vrai si l'unité enregistrée ne figure pas dans le référentiel. */
  private uniteHorsReferentiel(unite: string | undefined): boolean {
    if (!unite) return false;
    return !this.unites.some((u) => u.valeur === unite);
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
    this.uniteLibre = false;
    this.erreurFormulaire = '';
    this.cdr.markForCheck();
    this.afficherFormulaire = true;
  }

  ouvrirEdition(ind: Indicateur): void {
    this.modeEdition = true;
    this.indicateurCourant = { ...ind };
    this.uniteLibre = this.uniteHorsReferentiel(ind.unite);
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
