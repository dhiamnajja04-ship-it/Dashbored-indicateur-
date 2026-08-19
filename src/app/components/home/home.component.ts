import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IndicateurService, Indicateur } from '../../services/indicateur.service';
import { AnalyseIaComponent } from '../analyse-ia/analyse-ia.component';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterModule, AnalyseIaComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit {
  indicateurs: Indicateur[] = [];
  loading = true;
  erreur = '';

  totalIndicateurs = 0;
  totalValeurs = 0;
  valeursValidees = 0;
  valeursEnAttente = 0;

  /** Répartition des valeurs par statut, pour la barre de progression. */
  parStatut: Record<string, number> = {};

  /** Part des valeurs validées, en pourcentage entier. */
  get pourcentageValide(): number {
    if (this.totalValeurs === 0) return 0;
    return Math.round((this.valeursValidees / this.totalValeurs) * 100);
  }

  /**
   * Largeur d'un segment de la barre, en pourcentage.
   * Renvoie une chaîne CSS directement utilisable.
   */
  largeur(statut: string): string {
    if (this.totalValeurs === 0) return '0%';
    return ((this.parStatut[statut] ?? 0) / this.totalValeurs) * 100 + '%';
  }

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
        this.calculerStatistiques();
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Erreur lors du chargement des indicateurs', err);
        this.erreur =
          "Impossible de charger les indicateurs. Vérifie que le GatewayService (port 5169) et le MetierService (port 5039) sont bien démarrés.";
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  private calculerStatistiques(): void {
    this.totalIndicateurs = this.indicateurs.length;
    let totalValeurs = 0;
    let validees = 0;

    for (const indicateur of this.indicateurs) {
      const valeurs = indicateur.valeursIndicateurs ?? [];
      totalValeurs += valeurs.length;
      validees += valeurs.filter((v) => v.isValid).length;
    }

    this.totalValeurs = totalValeurs;
    this.valeursValidees = validees;
    this.valeursEnAttente = totalValeurs - validees;

    // Répartition détaillée : une valeur non validée n'est pas forcément un
    // brouillon, elle peut être en revue ou rejetée. La barre doit le montrer.
    const repartition: Record<string, number> = {
      Valide: 0,
      EnRevue: 0,
      Brouillon: 0,
      Rejete: 0,
    };
    for (const indicateur of this.indicateurs) {
      for (const v of indicateur.valeursIndicateurs ?? []) {
        const cle = v.isValid ? 'Valide' : (v.statut ?? 'Brouillon');
        repartition[cle] = (repartition[cle] ?? 0) + 1;
      }
    }
    this.parStatut = repartition;
  }
}
