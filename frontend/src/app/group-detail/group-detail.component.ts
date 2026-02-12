import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

@Component({
    selector: 'app-group-detail',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterModule],
    template: `
    <div class="container mx-auto p-4" *ngIf="group">
      <h1 class="text-3xl font-bold mb-2">{{ group.name }}</h1>
      <p class="text-gray-600 mb-4">{{ group.description }}</p>

      <div class="flex space-x-4 border-b mb-4">
        <button (click)="activeTab = 'members'" [class.border-blue-500]="activeTab === 'members'" class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">Members</button>
        <button (click)="activeTab = 'items'" [class.border-blue-500]="activeTab === 'items'" class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">Expenses</button>
        <button (click)="activeTab = 'voting'" [class.border-blue-500]="activeTab === 'voting'" class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">Voting</button>
      </div>

      <!-- Members Tab -->
      <div *ngIf="activeTab === 'members'">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-bold">Members</h2>
          <div class="flex gap-2">
            <input [(ngModel)]="newMemberEmail" placeholder="Email or Username" class="p-2 border rounded">
            <button (click)="addMember()" class="bg-green-500 text-white px-4 py-2 rounded">Add</button>
          </div>
        </div>
        <ul>
          <li *ngFor="let member of group.members" class="border-b p-2 flex justify-between">
            <span>{{ member.username }} ({{ member.role === 1 ? 'Admin' : 'Contributor' }})</span>
            <button *ngIf="isAdmin && member.userId !== currentUserId" (click)="removeMember(member.userId)" class="text-red-500 hover:underline">Remove</button>
          </li>
        </ul>
      </div>

      <!-- Expenses Tab -->
      <div *ngIf="activeTab === 'items'">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-bold">Expenses</h2>
          <button (click)="showAddItem = true" class="bg-blue-500 text-white px-4 py-2 rounded">Add Expense</button>
        </div>
        
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div>
            <h3 class="font-semibold mb-2">Transactions</h3>
            <ul>
              <li *ngFor="let item of items" class="border p-4 mb-2 rounded bg-white shadow-sm">
                <div class="flex justify-between">
                  <span class="font-bold">{{ item.title }}</span>
                  <span class="text-green-600">{{ item.amount | currency:item.currency }}</span>
                </div>
                <div class="text-sm text-gray-500">Paid by {{ item.paidByUsername }}</div>
                <button *ngIf="canEditItem(item)" (click)="deleteItem(item.id)" class="text-red-500 text-sm mt-2">Delete</button>
              </li>
            </ul>
          </div>
          <div>
            <h3 class="font-semibold mb-2">Balances</h3>
            <ul>
              <li *ngFor="let bal of balances" class="border-b p-2 flex justify-between">
                <span>{{ bal.username }}</span>
                <span [class.text-green-500]="bal.netBalance >= 0" [class.text-red-500]="bal.netBalance < 0">
                  {{ bal.netBalance | currency:bal.currency }}
                </span>
              </li>
            </ul>
          </div>
        </div>

        <!-- Add Item Modal -->
        <div *ngIf="showAddItem" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
          <div class="bg-white p-6 rounded shadow-lg w-full max-w-md">
            <h3 class="font-bold mb-4">Add Expense</h3>
            <input [(ngModel)]="newItem.title" placeholder="Title" class="w-full p-2 border rounded mb-2">
            <input [(ngModel)]="newItem.amount" type="number" placeholder="Amount" class="w-full p-2 border rounded mb-2">
            <input [(ngModel)]="newItem.currency" placeholder="Currency (USD)" class="w-full p-2 border rounded mb-2">
            <button (click)="addItem()" class="bg-blue-500 text-white px-4 py-2 rounded w-full">Save</button>
            <button (click)="showAddItem = false" class="mt-2 text-gray-500 w-full">Cancel</button>
          </div>
        </div>
      </div>

      <!-- Voting Tab -->
      <div *ngIf="activeTab === 'voting'">
        <div *ngIf="!votingSession">
          <p class="mb-4">No active voting session.</p>
          <button (click)="startVoting()" class="bg-blue-500 text-white px-4 py-2 rounded">Start Voting</button>
        </div>
        
        <div *ngIf="votingSession">
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-xl font-bold">Voting in Progress</h2>
             <button *ngIf="isAdmin" (click)="closeVoting()" class="bg-red-500 text-white px-4 py-2 rounded">Close Voting</button>
          </div>
          
          <div class="mb-6">
            <h3 class="font-semibold mb-2">Add Candidate</h3>
            <div class="flex gap-2">
              <input [(ngModel)]="newCandidate.cityName" placeholder="City" class="p-2 border rounded">
              <input [(ngModel)]="newCandidate.country" placeholder="Country" class="p-2 border rounded">
              <button (click)="addCandidate()" class="bg-green-500 text-white px-4 py-2 rounded">Add</button>
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div *ngFor="let candidate of votingSession.candidates" class="bg-white p-4 rounded shadow border">
              <div class="flex justify-between items-center">
                <div>
                    <h4 class="font-bold">{{ candidate.cityName }}, {{ candidate.country }}</h4>
                    <span class="text-sm text-gray-500">Votes: {{ candidate.voteCount }}</span>
                </div>
                <button (click)="vote(candidate.id)" class="bg-blue-100 text-blue-700 px-3 py-1 rounded hover:bg-blue-200">Vote</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class GroupDetailComponent implements OnInit {
    group: any;
    items: any[] = [];
    balances: any[] = [];
    votingSession: any;
    activeTab = 'members';
    newMemberEmail = '';
    showAddItem = false;
    newItem = { title: '', amount: 0, currency: 'USD' };
    newCandidate = { cityName: '', country: '' };

    private route = inject(ActivatedRoute);
    private http = inject(HttpClient);
    private apiUrl = 'http://localhost:5208/groups';
    currentUserId = localStorage.getItem('userId');

    get isAdmin() {
        return this.group?.members.find((m: any) => m.userId === this.currentUserId)?.role === 1;
    }

    ngOnInit() {
        this.route.params.subscribe(params => {
            const groupId = params['id'];
            if (groupId) {
                this.loadGroup(groupId);
                this.loadItems(groupId);
                this.loadBalances(groupId);
                this.loadVoting(groupId);
            }
        });
    }

    loadGroup(id: string) {
        this.http.get(`${this.apiUrl}/${id}`).subscribe(data => this.group = data);
    }

    addMember() {
        this.http.post(`${this.apiUrl}/${this.group.id}/members`, { emailOrUsername: this.newMemberEmail }).subscribe(() => {
            this.loadGroup(this.group.id);
            this.newMemberEmail = '';
        });
    }

    removeMember(userId: string) {
        this.http.delete(`${this.apiUrl}/${this.group.id}/members/${userId}`).subscribe(() => this.loadGroup(this.group.id));
    }

    loadItems(groupId: string) {
        this.http.get<any[]>(`${this.apiUrl}/${groupId}/items`).subscribe(data => this.items = data);
    }

    loadBalances(groupId: string) {
        this.http.get<any[]>(`${this.apiUrl}/${groupId}/balances`).subscribe(data => this.balances = data);
    }

    addItem() {
        this.http.post(`${this.apiUrl}/${this.group.id}/items`, this.newItem).subscribe(() => {
            this.showAddItem = false;
            this.newItem = { title: '', amount: 0, currency: 'USD' };
            this.loadItems(this.group.id);
            this.loadBalances(this.group.id);
        });
    }

    deleteItem(itemId: string) {
        this.http.delete(`${this.apiUrl}/${this.group.id}/items/${itemId}`).subscribe(() => {
            this.loadItems(this.group.id);
            this.loadBalances(this.group.id);
        });
    }

    canEditItem(item: any) {
        return this.isAdmin || item.paidByMemberId === this.currentUserId;
    }

    loadVoting(groupId: string) {
        this.http.get(`${this.apiUrl}/${groupId}/votings/active`).subscribe({
            next: (data) => this.votingSession = data,
            error: () => this.votingSession = null
        });
    }

    startVoting() {
        this.http.post(`${this.apiUrl}/${this.group.id}/votings`, { maxVotesPerMember: 3 }).subscribe(data => this.votingSession = data);
    }

    addCandidate() {
        this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/candidates`, this.newCandidate).subscribe(() => {
            this.loadVoting(this.group.id);
            this.newCandidate = { cityName: '', country: '' };
        });
    }

    vote(candidateId: string) {
        this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/votes`, { candidateId }).subscribe({
            next: () => this.loadVoting(this.group.id),
            error: (err) => alert(err.error || 'Vote failed')
        });
    }

    closeVoting() {
        this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/close`, {}).subscribe(() => {
            this.loadVoting(this.group.id);
            this.loadGroup(this.group.id);
        });
    }
}
