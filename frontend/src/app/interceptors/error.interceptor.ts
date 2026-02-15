import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err) => {
      // Deine API liefert typischerweise { code, message } oder ProblemDetails
      const msg =
        err?.error?.message ||
        err?.error?.detail ||     // ProblemDetails detail
        err?.error?.title ||      // ProblemDetails title
        (typeof err?.error === 'string' ? err.error : null) ||
        'Unexpected error';

      alert(msg);

      return throwError(() => err);
    })
  );
