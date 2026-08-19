import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { IndicateursListComponent } from './components/indicateurs-list/indicateurs-list.component';
import { ConsulterIndicateurComponent } from './components/consulter-indicateur/consulter-indicateur.component';
import { StatistiquesComponent } from './components/statistiques/statistiques.component';
import { ReclamationsComponent } from './components/reclamations/reclamations.component';

export const routes: Routes = [
  { path: '', component: HomeComponent, title: 'Tableau de bord — Pictor Solution' },
  {
    path: 'indicateurs',
    component: IndicateursListComponent,
    title: 'Indicateurs — Pictor Solution',
  },
  {
    path: 'indicateurs/:id',
    component: ConsulterIndicateurComponent,
    title: 'Détail indicateur — Pictor Solution',
  },
  {
    path: 'statistiques',
    component: StatistiquesComponent,
    title: 'Statistiques — Pictor Solution',
  },
  {
    path: 'reclamations',
    component: ReclamationsComponent,
    title: 'Réclamations — Pictor Solution',
  },
  { path: '**', redirectTo: '' },
];
