import { Routes } from '@angular/router';
import { HomeComponent } from './components/home/home.component';
import { IndicateursListComponent } from './components/indicateurs-list/indicateurs-list.component';
import { ConsulterIndicateurComponent } from './components/consulter-indicateur/consulter-indicateur.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'indicateurs', component: IndicateursListComponent },
  { path: 'indicateurs/:id', component: ConsulterIndicateurComponent },
  { path: '**', redirectTo: '' },
];
