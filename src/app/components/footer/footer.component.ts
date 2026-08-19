import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { SanteService, SantePlateforme } from '../../services/sante.service';

/**
 * Pied de page de la plateforme.
 *
 * Affiche l'état réel des services en interrogeant la sonde agrégée du
 * Gateway. Un pied de page qui dit « tout va bien » sans rien vérifier
 * n'apporte rien ; ici l'information est mesurée.
 */
@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './footer.component.html',
})
export class FooterComponent implements OnInit {
  private readonly sante = inject(SanteService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly anneeCourante = new Date().getFullYear();

  etat: SantePlateforme | null = null;
  sondeEnEchec = false;

  ngOnInit(): void {
    this.sante.etat().subscribe({
      next: (etat) => {
        this.etat = etat;
        this.cdr.markForCheck();
      },
      error: () => {
        // Le Gateway répond 503 quand un service interne est en panne : ce
        // n'est pas une erreur d'interface, c'est une information à afficher.
        this.sondeEnEchec = true;
        this.cdr.markForCheck();
      },
    });
  }

  classeEtat(valeur: string | undefined): string {
    return valeur === 'OK' ? 'service-ok' : 'service-ko';
  }

  remonterEnHaut(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
