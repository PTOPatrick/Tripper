import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { CurrencyService } from '../services/currency.service';

type MemberVm = {
  userId: string;      // Guid als string
  username: string;
  role: number;        // 1 = Admin
};

type BalanceVm = {
  userId: string;
  username: string;
  netBalance: number;
  currency: string; // CHF
};

type SettlementTransferVm = {
  fromUserId: string;
  fromUsername: string;
  toUserId: string;
  toUsername: string;
  amount: number;
  currency: string; // CHF
};

type SettlementSnapshotVm = {
  id: string;
  groupId: string;
  baseCurrency: string; // CHF
  createdAt: string;    // ISO
  createdByUserId: string;
  transfers: SettlementTransferVm[];
};

@Component({
  selector: 'app-group-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container mx-auto p-4" *ngIf="group">
      <h1 class="text-3xl font-bold mb-2">{{ group.name }}</h1>
      <p class="text-gray-600 mb-4">{{ group.description }}</p>

      <div class="flex space-x-4 border-b mb-4">
        <button (click)="activeTab = 'members'"
                [class.border-blue-500]="activeTab === 'members'"
                class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">
          Members
        </button>
        <button (click)="activeTab = 'items'"
                [class.border-blue-500]="activeTab === 'items'"
                class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">
          Expenses
        </button>
        <button (click)="activeTab = 'voting'"
                [class.border-blue-500]="activeTab === 'voting'"
                class="py-2 px-4 border-b-2 border-transparent hover:border-gray-300">
          Voting
        </button>
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
            <button *ngIf="isAdmin && member.userId !== currentUserId"
                    (click)="removeMember(member.userId)"
                    class="text-red-500 hover:underline">
              Remove
            </button>
          </li>
        </ul>
      </div>

      <!-- Expenses Tab -->
      <div *ngIf="activeTab === 'items'">
        <div class="flex justify-between items-center mb-4">
          <h2 class="text-xl font-bold">Expenses</h2>
          <button (click)="openAddItem()" class="bg-blue-500 text-white px-4 py-2 rounded">Add Expense</button>
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
                <div *ngIf="item.description" class="text-sm text-gray-600 mt-1">{{ item.description }}</div>
                <button *ngIf="canEditItem(item)" (click)="deleteItem(item.id)" class="text-red-500 text-sm mt-2">
                  Delete
                </button>
              </li>
            </ul>
          </div>

          <div>
            <div class="flex items-center justify-between mb-2">
              <h3 class="font-semibold">Balances</h3>

              <!-- Actions -->
              <div class="flex items-center gap-2">
                <!-- ✅ Show/Hide balances -->
                <button
                  type="button"
                  (click)="toggleBalances()"
                  class="px-3 py-2 rounded border text-sm hover:bg-gray-50">
                  {{ showBalances ? 'Hide balances' : 'Show balances' }}
                </button>

                <button
                  type="button"
                  (click)="recalculateSettlement()"
                  [disabled]="settlementLoading"
                  class="bg-gray-900 text-white px-3 py-2 rounded text-sm disabled:opacity-50">
                  {{ settlementLoading ? 'Recalculating…' : 'Recalculate settlement' }}
                </button>

                <button
                  type="button"
                  (click)="toggleSettlements()"
                  [disabled]="!settlementSnapshot || settlementLoading"
                  class="px-3 py-2 rounded border text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50">
                  {{ showSettlements ? 'Hide settlements' : 'Show settlements' }}
                </button>
              </div>
            </div>

            <!-- ✅ Balances list (toggleable) -->
            <ul class="mb-2" *ngIf="showBalances">
              <li *ngFor="let bal of balances" class="border-b p-2 flex justify-between">
                <span>{{ bal.username }}</span>
                <span [class.text-green-500]="bal.netBalance >= 0" [class.text-red-500]="bal.netBalance < 0">
                  {{ bal.netBalance | currency:bal.currency }}
                </span>
              </li>
            </ul>

            <!-- Settlement metadata + errors -->
            <div class="text-xs text-gray-500 mb-2" *ngIf="settlementSnapshot">
              Last recalculated at:
              <span class="font-medium">{{ formatDateTime(settlementSnapshot.createdAt) }}</span>
            </div>

            <div *ngIf="settlementError" class="mb-2 p-2 rounded border border-red-300 bg-red-50 text-red-700 text-sm">
              {{ settlementError }}
            </div>

            <!-- Settlements list -->
            <div *ngIf="showSettlements && settlementSnapshot" class="mt-3">
              <h4 class="font-semibold mb-2">Settlements</h4>

              <div *ngIf="(settlementSnapshot?.transfers?.length ?? 0) === 0"
                   class="text-sm text-gray-500 border rounded p-3 bg-white">
                No transfers needed.
              </div>

              <ul *ngIf="(settlementSnapshot?.transfers?.length ?? 0) > 0">
                <li *ngFor="let t of settlementSnapshot.transfers"
                    class="border p-3 mb-2 rounded bg-white shadow-sm">
                  <div class="flex justify-between items-center">
                    <div class="text-sm">
                      <span class="font-semibold">{{ t.fromUsername }}</span>
                      <span class="mx-1">→</span>
                      <span class="font-semibold">{{ t.toUsername }}</span>
                    </div>
                    <div class="font-semibold">
                      {{ t.amount | currency:t.currency }}
                    </div>
                  </div>
                </li>
              </ul>
            </div>

          </div>
        </div>

        <!-- Add Item Modal -->
        <div *ngIf="showAddItem" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4">
          <div class="bg-white p-6 rounded shadow-lg w-full max-w-md">
            <h3 class="font-bold mb-4">Add Expense</h3>

            <div *ngIf="addItemError" class="mb-3 p-2 rounded border border-red-300 bg-red-50 text-red-700 text-sm">
              {{ addItemError }}
            </div>

            <input [(ngModel)]="newItem.title" placeholder="Title" class="w-full p-2 border rounded mb-2">
            <input [(ngModel)]="newItem.amount" type="number" placeholder="Amount" class="w-full p-2 border rounded mb-2">

            <label class="block text-sm text-gray-600 mb-1">Description (optional)</label>
            <textarea
              [(ngModel)]="newItem.description"
              rows="3"
              placeholder="Add details..."
              class="w-full p-2 border rounded mb-3 resize-none"></textarea>

            <label class="block text-sm text-gray-600 mb-1">Paid by</label>
            <select [(ngModel)]="newItem.paidByMemberId" class="w-full p-2 border rounded mb-3">
              <option value="" disabled>Select member…</option>
              <option *ngFor="let m of group.members" [value]="m.userId">
                {{ m.username }}
              </option>
            </select>

            <div class="flex items-center justify-between mb-1">
              <label class="block text-sm text-gray-600">Payees (who must pay)</label>
              <button type="button"
                      (click)="toggleSelectAllPayees()"
                      class="text-sm text-blue-600 hover:underline">
                {{ areAllPayeesSelected ? 'Deselect all' : 'Select all' }}
              </button>
            </div>

            <div class="border rounded p-2 mb-3 max-h-40 overflow-auto">
              <label *ngFor="let m of group.members" class="flex items-center gap-2 py-1">
                <input type="checkbox"
                       [checked]="isPayeeSelected(m.userId)"
                       (change)="togglePayee(m.userId, $event)" />
                <span>{{ m.username }}</span>
              </label>
            </div>

            <label class="block text-sm text-gray-600 mb-1">Currency</label>
            <div class="relative mb-2">
              <input
                [(ngModel)]="currencyQuery"
                name="currencyQuery"
                (focus)="currencyOpen = true; filterCurrencies()"
                (input)="filterCurrencies(); currencyOpen = true"
                (keydown)="onCurrencyKeyDown($event)"
                placeholder="Search currency (e.g. CHF)"
                class="w-full p-2 border rounded"
                autocomplete="off"
              />

              <div *ngIf="currencyOpen"
                   class="absolute z-50 mt-1 w-full max-h-56 overflow-auto rounded border bg-white shadow">
                <button
                  type="button"
                  *ngFor="let c of filteredCurrencies; let i = index"
                  (click)="selectCurrency(c)"
                  class="w-full text-left px-3 py-2 hover:bg-gray-100"
                  [class.bg-gray-100]="i === currencyActiveIndex">
                  {{ c }}
                </button>

                <div *ngIf="filteredCurrencies.length === 0" class="px-3 py-2 text-sm text-gray-500">
                  No matches
                </div>
              </div>
            </div>

            <div *ngIf="currencyLoading" class="text-xs text-gray-500 mb-2">Loading currencies…</div>

            <button (click)="addItem()" class="bg-blue-500 text-white px-4 py-2 rounded w-full">Save</button>
            <button (click)="closeAddItem()" class="mt-2 text-gray-500 w-full">Cancel</button>
          </div>
        </div>
      </div>

      <!-- Voting Tab -->
      <div *ngIf="activeTab === 'voting'">
        <div *ngIf="!votingSession">
          <p class="mb-4">No active voting session.</p>
          <div class="flex items-center gap-2 mb-4">
            <label class="text-sm text-gray-600">Max votes per member</label>
            <input type="number" min="1" max="10" step="1" [(ngModel)]="maxVotesPerMember" class="p-2 border rounded w-24"/>
          </div>
          <button (click)="startVoting()" class="bg-blue-500 text-white px-4 py-2 rounded">Start Voting</button>
        </div>

        <div *ngIf="votingSession">
          <div class="flex justify-between items-center mb-4">
            <h2 class="text-xl font-bold">Voting in Progress</h2>
            <button *ngIf="isAdmin" (click)="closeVoting()" class="bg-red-500 text-white px-4 py-2 rounded">Close Voting</button>
          </div>

          <div class="mb-6">
            <h3 class="font-semibold mb-2">Add Candidate</h3>
            <form class="flex gap-2 mb-6" (ngSubmit)="addCandidate()">
              <input [(ngModel)]="newCandidate.cityName" name="cityName" placeholder="City" class="p-2 border rounded" />
              <input [(ngModel)]="newCandidate.country" name="country" placeholder="Country" class="p-2 border rounded" />
              <button type="submit" class="bg-green-500 text-white px-4 py-2 rounded">Add</button>
            </form>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div *ngFor="let candidate of votingSession.candidates" class="bg-white p-4 rounded shadow border">
              <div class="flex justify-between items-center gap-4">
                <div>
                  <h4 class="font-bold">{{ candidate.cityName }}, {{ candidate.country }}</h4>
                  <div class="text-sm text-gray-500">Total votes: {{ candidate.voteCount }}</div>

                  <div class="mt-1 text-xs text-gray-500">
                    Your votes: <span class="font-semibold">{{ candidate.myVoteCount || 0 }}</span>
                    <span class="mx-2">•</span>
                    Remaining: <span class="font-semibold">{{ remainingVotes }}</span>
                    <span class="mx-2">•</span>
                    Max: <span class="font-semibold">{{ votingSession.maxVotesPerMember }}</span>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <button (click)="unvote(candidate.id)"
                          [disabled]="(candidate.myVoteCount || 0) <= 0"
                          class="px-3 py-2 rounded border text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50">
                    −
                  </button>

                  <span class="w-8 text-center font-semibold">{{ candidate.myVoteCount || 0 }}</span>

                  <button (click)="vote(candidate.id)"
                          [disabled]="remainingVotes <= 0"
                          class="px-3 py-2 rounded border text-sm disabled:opacity-40 disabled:cursor-not-allowed hover:bg-gray-50">
                    +
                  </button>
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>

    </div>
  `
})
export class GroupDetailComponent implements OnInit {
  group: { id: string; name: string; description: string; members: MemberVm[] } | null = null;

  items: any[] = [];
  balances: BalanceVm[] = [];
  votingSession: any;

  activeTab = 'members';
  newMemberEmail = '';

  // Modal state
  showAddItem = false;
  addItemError = '';

  // ✅ Balances toggle
  showBalances = true;

  // ✅ Settlement state
  settlementSnapshot: SettlementSnapshotVm | null = null;
  showSettlements = false;
  settlementLoading = false;
  settlementError = '';

  // Expense DTO fields (frontend view-model)
  newItem: {
    title: string;
    amount: number;
    currency: string;
    description: string;
    paidByMemberId: string;
    payeeUserIds: string[];
  } = {
    title: '',
    amount: 0,
    currency: '',
    description: '',
    paidByMemberId: '',
    payeeUserIds: []
  };

  selectedPayees = new Set<string>();

  // Voting
  newCandidate = { cityName: '', country: '' };
  maxVotesPerMember = 3;

  // Currency dropdown
  currencies: string[] = [];
  currencyLoading = false;
  currencyQuery = '';
  currencyOpen = false;
  filteredCurrencies: string[] = [];
  currencyActiveIndex = -1;

  private currencyService = inject(CurrencyService);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private apiUrl = 'http://localhost:5208/groups';

  currentUserId: string = localStorage.getItem('userId') ?? '';

  get isAdmin() {
    return this.group?.members.find(m => m.userId === this.currentUserId)?.role === 1;
  }

  ngOnInit() {
    this.currencyLoading = true;
    this.currencyService.getCurrencies().subscribe({
      next: (codes) => {
        this.currencies = codes;
        this.currencyLoading = false;
      },
      error: () => {
        this.currencyLoading = false;
        this.currencies = ['CHF', 'EUR', 'USD'];
      }
    });

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

  // ✅ toggle balances
  toggleBalances() {
    this.showBalances = !this.showBalances;
  }

  // -------- Currency dropdown --------
  filterCurrencies() {
    const q = (this.currencyQuery || '').trim().toUpperCase();
    const all = this.currencies || [];
    this.filteredCurrencies =
      q.length === 0
        ? all.slice(0, 50)
        : all.filter(c => c.includes(q)).slice(0, 50);

    this.currencyActiveIndex = this.filteredCurrencies.length > 0 ? 0 : -1;
  }

  selectCurrency(code: string) {
    this.currencyQuery = code;
    this.newItem.currency = code;
    this.currencyOpen = false;
  }

  onCurrencyKeyDown(ev: KeyboardEvent) {
    if (!this.currencyOpen && (ev.key === 'ArrowDown' || ev.key === 'ArrowUp')) {
      this.currencyOpen = true;
      this.filterCurrencies();
      ev.preventDefault();
      return;
    }

    if (!this.currencyOpen) return;

    if (ev.key === 'Escape') {
      this.currencyOpen = false;
      return;
    }

    if (ev.key === 'ArrowDown') {
      ev.preventDefault();
      if (this.filteredCurrencies.length === 0) return;
      this.currencyActiveIndex = Math.min(this.currencyActiveIndex + 1, this.filteredCurrencies.length - 1);
      return;
    }

    if (ev.key === 'ArrowUp') {
      ev.preventDefault();
      if (this.filteredCurrencies.length === 0) return;
      this.currencyActiveIndex = Math.max(this.currencyActiveIndex - 1, 0);
      return;
    }

    if (ev.key === 'Enter') {
      ev.preventDefault();
      if (this.currencyActiveIndex >= 0 && this.currencyActiveIndex < this.filteredCurrencies.length) {
        this.selectCurrency(this.filteredCurrencies[this.currencyActiveIndex]);
      } else {
        const typed = (this.currencyQuery || '').trim().toUpperCase();
        if (this.currencies.includes(typed)) this.selectCurrency(typed);
      }
    }
  }

  // -------- Payees multi-select --------
  isPayeeSelected(userId: string) {
    return this.selectedPayees.has(userId);
  }

  togglePayee(userId: string, ev: Event) {
    const checked = (ev.target as HTMLInputElement).checked;
    if (checked) this.selectedPayees.add(userId);
    else this.selectedPayees.delete(userId);
  }

  get areAllPayeesSelected(): boolean {
    const members = this.group?.members ?? [];
    if (members.length === 0) return false;
    return members.every(m => this.selectedPayees.has(m.userId));
  }

  toggleSelectAllPayees() {
    const members = this.group?.members ?? [];
    if (members.length === 0) return;

    if (this.areAllPayeesSelected) {
      this.selectedPayees.clear();
    } else {
      for (const m of members) this.selectedPayees.add(m.userId);
    }
  }

  // -------- Modal open/close --------
  openAddItem() {
    this.showAddItem = true;
    this.addItemError = '';

    this.newItem = {
      title: '',
      amount: 0,
      currency: '',
      description: '',
      paidByMemberId: this.currentUserId || '',
      payeeUserIds: []
    };

    this.selectedPayees.clear();

    this.currencyQuery = (this.newItem.currency || '').toUpperCase();
    this.currencyOpen = false;
    this.filterCurrencies();
  }

  closeAddItem() {
    this.showAddItem = false;
    this.addItemError = '';
    this.currencyOpen = false;
  }

  // -------- Settlement UI actions --------
  toggleSettlements() {
    this.showSettlements = !this.showSettlements;
  }

  recalculateSettlement() {
    if (!this.group) return;

    this.settlementLoading = true;
    this.settlementError = '';

    this.http.post<SettlementSnapshotVm>(`${this.apiUrl}/${this.group.id}/settlement/recalculate`, {})
      .subscribe({
        next: (snap) => {
          this.settlementSnapshot = snap;
          this.showSettlements = true;
          this.settlementLoading = false;

          this.loadBalances(this.group!.id);
        },
        error: (err) => {
          const msg =
            err?.error?.message ||
            err?.error?.title ||
            err?.message ||
            'Could not recalculate settlement.';
          this.settlementError = msg;
          this.settlementLoading = false;
        }
      });
  }

  formatDateTime(iso: string): string {
    try {
      const d = new Date(iso);
      return d.toLocaleString();
    } catch {
      return iso;
    }
  }

  // -------- API calls --------
  loadGroup(id: string) {
    this.http.get<any>(`${this.apiUrl}/${id}`).subscribe(data => this.group = data);
  }

  addMember() {
    if (!this.group) return;
    this.http.post(`${this.apiUrl}/${this.group.id}/members`, { emailOrUsername: this.newMemberEmail }).subscribe(() => {
      this.loadGroup(this.group!.id);
      this.newMemberEmail = '';
    });
  }

  removeMember(userId: string) {
    if (!this.group) return;
    this.http.delete(`${this.apiUrl}/${this.group.id}/members/${userId}`).subscribe(() => this.loadGroup(this.group!.id));
  }

  loadItems(groupId: string) {
    this.http.get<any[]>(`${this.apiUrl}/${groupId}/items`).subscribe(data => this.items = data);
  }

  loadBalances(groupId: string) {
    this.http.get<BalanceVm[]>(`${this.apiUrl}/${groupId}/balances`).subscribe(data => this.balances = data);
  }

  addItem() {
    if (!this.group) return;

    this.addItemError = '';

    const title = (this.newItem.title || '').trim();
    const amount = Number(this.newItem.amount);

    if (!title) {
      this.addItemError = 'Please enter a title.';
      return;
    }

    if (!Number.isFinite(amount) || amount <= 0) {
      this.addItemError = 'Please enter an amount greater than 0.';
      return;
    }

    if (!this.newItem.paidByMemberId) {
      this.addItemError = 'Please select who paid.';
      return;
    }

    const payees = Array.from(this.selectedPayees);
    if (payees.length < 1) {
      this.addItemError = 'Please select at least one payee.';
      return;
    }

    const currency = (this.currencyQuery || this.newItem.currency || 'CHF').trim().toUpperCase();
    const description = (this.newItem.description || '').trim();

    const body = {
      title,
      amount,
      currency,
      description,
      paidByMemberId: this.newItem.paidByMemberId,
      payeeUserIds: payees
    };

    this.http.post(`${this.apiUrl}/${this.group.id}/items`, body).subscribe({
      next: () => {
        this.showAddItem = false;
        this.loadItems(this.group!.id);
        this.loadBalances(this.group!.id);
      },
      error: (err) => {
        const msg =
          err?.error?.message ||
          err?.error?.title ||
          err?.message ||
          'Could not create expense.';
        this.addItemError = msg;
      }
    });
  }

  deleteItem(itemId: string) {
    if (!this.group) return;
    this.http.delete(`${this.apiUrl}/${this.group.id}/items/${itemId}`).subscribe(() => {
      this.loadItems(this.group!.id);
      this.loadBalances(this.group!.id);
    });
  }

  canEditItem(item: any) {
    return this.isAdmin || item.paidByMemberId === this.currentUserId;
  }

  // -------- Voting (unverändert) --------
  loadVoting(groupId: string) {
    this.http.get(`${this.apiUrl}/${groupId}/votings/active`).subscribe({
      next: (data) => this.votingSession = data,
      error: () => this.votingSession = null
    });
  }

  startVoting() {
    if (!this.group) return;
    const maxVotes = Math.max(1, Math.floor(Number(this.maxVotesPerMember) || 1));
    this.http.post(`${this.apiUrl}/${this.group.id}/votings`, { maxVotesPerMember: maxVotes })
      .subscribe(data => this.votingSession = data);
  }

  addCandidate() {
    if (!this.group || !this.votingSession) return;

    const city = (this.newCandidate.cityName || '').trim();
    const country = (this.newCandidate.country || '').trim();
    if (!city || !country) return;

    this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/candidates`, { cityName: city, country })
      .subscribe(() => {
        this.loadVoting(this.group!.id);
        this.newCandidate = { cityName: '', country: '' };
      });
  }

  vote(candidateId: string) {
    if (!this.group || !this.votingSession) return;
    this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/votes`, { candidateId })
      .subscribe(() => this.loadVoting(this.group!.id));
  }

  unvote(candidateId: string) {
    if (!this.group || !this.votingSession) return;
    this.http.delete(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/votes/${candidateId}`)
      .subscribe(() => this.loadVoting(this.group!.id));
  }

  get totalMyVotes(): number {
    if (!this.votingSession?.candidates) return 0;
    return this.votingSession.candidates.reduce((sum: number, c: any) => sum + (c.myVoteCount || 0), 0);
  }

  get remainingVotes(): number {
    const max = this.votingSession?.maxVotesPerMember ?? 0;
    return Math.max(0, max - this.totalMyVotes);
  }

  closeVoting() {
    if (!this.group || !this.votingSession) return;
    this.http.post(`${this.apiUrl}/${this.group.id}/votings/${this.votingSession.id}/close`, {}).subscribe(() => {
      this.loadVoting(this.group!.id);
      this.loadGroup(this.group!.id);
    });
  }
}
