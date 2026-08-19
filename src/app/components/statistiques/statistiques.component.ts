import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IndicateurService, Indicateur, ValeurIndicateur } from '../../services/indicateur.service';

interface EcartIndicateur {
  code: string;
  nom: string;
  unite: string;
  valeur: number;
  cible: number;
  /** Écart relatif à la cible, en pourcentage. */
  ecartRelatif: number;
  /** Largeur du segment, en pourcentage de la demi-largeur du graphique. */
  largeur: number;
  sens: 'au-dessus' | 'en-dessous' | 'atteinte';
}

interface Part {
  libelle: string;
  nombre: number;
  largeur: number;
}

/**
 * Page de statistiques.
 *
 * Les formes sont choisies avant les couleurs :
 *  - l'écart à la cible est une POLARITÉ (au-dessus / en-dessous) -> barres
 *    divergentes centrées sur la cible ;
 *  - la fiabilité est une MAGNITUDE sur une échelle ordonnée -> rampe d'une
 *    seule teinte, du clair au foncé ;
 *  - le territoire ne compte que deux classes -> des chiffres, pas un graphique.
 */
@Component({
  selector: 'app-statistiques',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './statistiques.component.html',
})
export class StatistiquesComponent implements OnInit {
  indicateurs: Indicateur[] = [];
  loading = true;
  erreur = '';

  totalIndicateurs = 0;
  totalValeurs = 0;
  valeursValidees = 0;

  ecarts: EcartIndicateur[] = [];
  fiabilites: Part[] = [];
  valeursNationales = 0;
  valeursRegionales = 0;
  gouvernoratsCouverts: string[] = [];

  /** Bascule vers la vue tabulaire : un graphique doit toujours avoir une alternative lisible. */
  vueTableau = false;

  constructor(
    private indicateurService: IndicateurService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.charger();
  }

  charger(): void {
    this.loading = true;
    this.erreur = '';
    this.cdr.markForCheck();
    this.indicateurService.getAll().subscribe({
      next: (data) => {
        this.indicateurs = data;
        this.calculer();
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Erreur lors du chargement des statistiques', err);
        this.erreur = 'Impossible de charger les statistiques.';
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  basculerVue(): void {
    this.vueTableau = !this.vueTableau;
  }

  private toutesLesValeurs(): ValeurIndicateur[] {
    return this.indicateurs.flatMap((i) => i.valeursIndicateurs ?? []);
  }

  private calculer(): void {
    const valeurs = this.toutesLesValeurs();
    this.totalIndicateurs = this.indicateurs.length;
    this.totalValeurs = valeurs.length;
    this.valeursValidees = valeurs.filter((v) => v.isValid).length;

    this.calculerEcarts();
    this.calculerFiabilites(valeurs);
    this.calculerTerritoires(valeurs);
  }

  /**
   * Écart à la cible, exprimé en POURCENTAGE de la cible et non en valeur
   * absolue : les indicateurs n'ont pas la même unité (%, pour 10 000 hab.),
   * comparer des écarts bruts n'aurait aucun sens.
   */
  private calculerEcarts(): void {
    const lignes: EcartIndicateur[] = [];

    for (const ind of this.indicateurs) {
      const cible = ind.valeurCible;
      if (cible === null || cible === undefined || Number(cible) === 0) continue;

      const valeursInd = ind.valeursIndicateurs ?? [];
      if (valeursInd.length === 0) continue;

      // On privilégie une valeur validée : c'est celle qui fait référence.
      const retenue = valeursInd.find((v) => v.isValid) ?? valeursInd[0];
      const valeur = Number(retenue.valeur);
      const ecartRelatif = ((valeur - Number(cible)) / Number(cible)) * 100;
      const arrondi = Math.round(ecartRelatif * 10) / 10;

      lignes.push({
        code: ind.code,
        nom: ind.nom,
        unite: ind.unite ?? '',
        valeur,
        cible: Number(cible),
        ecartRelatif: arrondi,
        largeur: 0,
        sens: arrondi === 0 ? 'atteinte' : arrondi > 0 ? 'au-dessus' : 'en-dessous',
      });
    }

    // Échelle commune aux deux côtés, sinon les longueurs ne sont pas comparables.
    const maxi = Math.max(1, ...lignes.map((l) => Math.abs(l.ecartRelatif)));
    for (const l of lignes) {
      l.largeur = (Math.abs(l.ecartRelatif) / maxi) * 100;
    }

    lignes.sort((a, b) => b.ecartRelatif - a.ecartRelatif);
    this.ecarts = lignes;
  }

  private calculerFiabilites(valeurs: ValeurIndicateur[]): void {
    const ordre = ['haute', 'moyenne', 'faible'];
    const compte = new Map<string, number>(ordre.map((o) => [o, 0]));
    let nonRenseigne = 0;

    for (const v of valeurs) {
      const d = (v.degreDeFiabilite ?? '').toLowerCase();
      if (compte.has(d)) compte.set(d, (compte.get(d) ?? 0) + 1);
      else nonRenseigne++;
    }

    const maxi = Math.max(1, ...[...compte.values()], nonRenseigne);
    const parts: Part[] = ordre.map((o) => ({
      libelle: o.charAt(0).toUpperCase() + o.slice(1),
      nombre: compte.get(o) ?? 0,
      largeur: ((compte.get(o) ?? 0) / maxi) * 100,
    }));
    if (nonRenseigne > 0) {
      parts.push({
        libelle: 'Non renseignée',
        nombre: nonRenseigne,
        largeur: (nonRenseigne / maxi) * 100,
      });
    }
    this.fiabilites = parts;
  }

  private calculerTerritoires(valeurs: ValeurIndicateur[]): void {
    this.valeursRegionales = valeurs.filter((v) => !!v.gouvernorat).length;
    this.valeursNationales = valeurs.length - this.valeursRegionales;
    this.gouvernoratsCouverts = [
      ...new Set(valeurs.map((v) => v.gouvernorat).filter((g): g is string => !!g)),
    ].sort();
  }
}
