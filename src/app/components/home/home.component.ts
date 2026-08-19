import { Component, OnInit } from '@angular/core';
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

  constructor(private indicateurService: IndicateurService) {}

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.loading = true;
    this.erreur = '';
    this.indicateurService.getAll().subscribe({
      next: (data) => {
        this.indicateurs = data;
        this.calculerStatistiques();
        this.loading = false;
      },
      error: (err) => {
        console.error('Erreur lors du chargement des indicateurs', err);
        this.erreur =
          "Impossible de charger les indicateurs. Vérifie que le GatewayService (port 5169) et le MetierService (port 5039) sont bien démarrés.";
        this.loading = false;
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
  }
}
