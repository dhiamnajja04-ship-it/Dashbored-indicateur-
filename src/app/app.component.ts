import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NotificationsComponent } from './components/notifications/notifications.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterModule, NotificationsComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  title = 'dashboard-indicateurs';
  readonly anneeCourante = new Date().getFullYear();
}
