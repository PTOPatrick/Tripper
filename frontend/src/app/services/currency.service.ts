import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, tap } from 'rxjs';

type CurrencyCache = { savedAt: number; codes: string[] };

@Injectable({ providedIn: 'root' })
export class CurrencyService {
  private readonly cacheKey = 'tripper.currencies.v1';
  private readonly ttlMs = 6 * 60 * 60 * 1000;
  private readonly apiBase = 'http://localhost:5208';

  constructor(private http: HttpClient) {}

  getCurrencies(): Observable<string[]> {
    const cached = this.readCache();
    if (cached) return of(cached.codes);

    return this.http.get<string[]>(`${this.apiBase}/currencies`).pipe(
      tap(codes => this.writeCache(codes))
    );
  }

  private readCache(): CurrencyCache | null {
    try {
      const raw = sessionStorage.getItem(this.cacheKey);
      if (!raw) return null;

      const parsed = JSON.parse(raw) as CurrencyCache;
      if (!parsed?.codes?.length) return null;

      const age = Date.now() - parsed.savedAt;
      if (age > this.ttlMs) return null;

      return parsed;
    } catch {
      return null;
    }
  }

  private writeCache(codes: string[]) {
    const payload: CurrencyCache = { savedAt: Date.now(), codes };
    sessionStorage.setItem(this.cacheKey, JSON.stringify(payload));
  }
}
