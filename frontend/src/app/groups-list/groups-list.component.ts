import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-groups-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="container mx-auto p-4">
      <div class="flex justify-between items-center mb-6">
        <h1 class="text-3xl font-bold">My Groups</h1>
        <button (click)="showCreateModal = true" class="bg-blue-500 text-white px-4 py-2 rounded hover:bg-blue-600">
          Create New Group
        </button>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        <div *ngFor="let group of groups" [routerLink]="['/groups', group.id]" class="bg-white p-6 rounded shadow cursor-pointer hover:shadow-lg transition">
          <h2 class="text-xl font-semibold mb-2">{{ group.name }}</h2>
          <p class="text-gray-600 mb-4">{{ group.description }}</p>
          <div class="flex justify-between text-sm text-gray-500">
            <span>{{ group.memberCount }} members</span>
            <span *ngIf="group.destinationCityName">📍 {{ group.destinationCityName }}</span>
          </div>
        </div>
      </div>

      <!-- Create Modal -->
      <div *ngIf="showCreateModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
        <div class="bg-white p-6 rounded shadow-lg w-full max-w-md">
          <h2 class="text-xl font-bold mb-4">Create Group</h2>
          <form (ngSubmit)="createGroup()">
            <div class="mb-4">
              <label class="block text-gray-700 mb-2">Name</label>
              <input [(ngModel)]="newGroup.name" name="name" type="text" class="w-full p-2 border rounded" required>
            </div>
            <div class="mb-4">
              <label class="block text-gray-700 mb-2">Description</label>
              <textarea [(ngModel)]="newGroup.description" name="description" class="w-full p-2 border rounded"></textarea>
            </div>
            <div class="mb-4 flex gap-2">
              <div class="w-1/2">
                <label class="block text-gray-700 mb-2">City</label>
                <input [(ngModel)]="newGroup.destinationCityName" name="city" type="text" class="w-full p-2 border rounded">
              </div>
              <div class="w-1/2">
                <label class="block text-gray-700 mb-2">Country</label>
                <input [(ngModel)]="newGroup.destinationCountry" name="country" type="text" class="w-full p-2 border rounded">
              </div>
            </div>
            <div class="flex justify-end gap-2">
              <button type="button" (click)="showCreateModal = false" class="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded">Cancel</button>
              <button type="submit" class="bg-blue-500 text-white px-4 py-2 rounded hover:bg-blue-600">Create</button>
            </div>
          </form>
        </div>
      </div>
    </div>
  `
})
export class GroupsListComponent implements OnInit {
  groups: any[] = [];
  showCreateModal = false;
  newGroup = { name: '', description: '', destinationCityName: '', destinationCountry: '' };
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5208/groups';

  ngOnInit() {
    this.loadGroups();
  }

  loadGroups() {
    this.http.get<any[]>(this.apiUrl).subscribe(data => this.groups = data);
  }

  createGroup() {
    this.http.post(this.apiUrl, this.newGroup).subscribe({
      next: () => {
        this.showCreateModal = false;
        this.newGroup = { name: '', description: '', destinationCityName: '', destinationCountry: '' };
        this.loadGroups();
      },
      error: (err) => alert('Failed to create group')
    });
  }
}
