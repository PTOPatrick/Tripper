using Microsoft.EntityFrameworkCore;
using Tripper.Core.Entities;
using Tripper.Core.Interfaces;

namespace Tripper.Infra.Data;

public static class TripperDbSeeder
{
    public static async Task SeedAsync(TripperDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        // -----------------------------
        // CURRENCIES (Lookup table)
        // -----------------------------
        var currencyCodes = new[]
        {
            "AED","AFN","ALL","AMD","ANG","AOA","ARS","AUD","AWG","AZN",
            "BAM","BBD","BDT","BGN","BHD","BIF","BMD","BND","BOB","BRL",
            "BSD","BTN","BWP","BYN","BZD",
            "CAD","CDF","CHF","CLF","CLP","CNH","CNY","COP","CRC","CUP","CVE","CZK",
            "DJF","DKK","DOP","DZD",
            "EGP","ERN","ETB","EUR",
            "FJD","FKP","FOK",
            "GBP","GEL","GGP","GHS","GIP","GMD","GNF","GTQ","GYD",
            "HKD","HNL","HRK","HTG","HUF",
            "IDR","ILS","IMP","INR","IQD","IRR","ISK",
            "JEP","JMD","JOD","JPY",
            "KES","KGS","KHR","KID","KMF","KRW","KWD","KYD","KZT",
            "LAK","LBP","LKR","LRD","LSL","LYD",
            "MAD","MDL","MGA","MKD","MMK","MNT","MOP","MRU","MUR","MVR","MWK","MXN","MYR","MZN",
            "NAD","NGN","NIO","NOK","NPR","NZD",
            "OMR",
            "PAB","PEN","PGK","PHP","PKR","PLN","PYG",
            "QAR",
            "RON","RSD","RUB","RWF",
            "SAR","SBD","SCR","SDG","SEK","SGD","SHP","SLE","SLL","SOS","SRD","SSP","STN","SYP","SZL",
            "THB","TJS","TMT","TND","TOP","TRY","TTD","TVD","TWD","TZS",
            "UAH","UGX","USD","UYU","UZS",
            "VES","VND","VUV",
            "WST",
            "XAF","XCD","XCG","XDR","XOF","XPF",
            "YER",
            "ZAR","ZMW","ZWG","ZWL"
        };

        var wanted = currencyCodes
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

        var existing = await db.Currencies
            .AsNoTracking()
            .Select(c => c.Code)
            .ToListAsync(cancellationToken: ct);

        var existingSet = existing
            .Select(c => (c ?? "").Trim().ToUpperInvariant())
            .ToHashSet();

        var missing = wanted
            .Where(code => !existingSet.Contains(code))
            .ToList();

        if (missing.Count > 0)
        {
            db.Currencies.AddRange(missing.Select(code => new Core.Entities.Currency
            {
                Code = code
            }));

            await db.SaveChangesAsync(ct);
        }

        // --- Guard: Do not reseed ---
        if (await db.Users.AnyAsync(ct))
            return;

        var rng = new Random();

        // -----------------------------
        // USERS (100)
        // -----------------------------
        var users = Enumerable.Range(1, 100)
            .Select(i => new User
            {
                Id = Guid.NewGuid(),
                Username = $"user{i}",
                Email = $"user{i}@mail.com",
                PasswordHash = hasher.HashPassword("Password123!")
            })
            .ToList();

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        // -----------------------------
        // REAL CITIES
        // -----------------------------
        var cities = new (string City, string Country)[]
        {
            ("Zurich","Switzerland"),("Geneva","Switzerland"),("Bern","Switzerland"),
            ("Paris","France"),("Lyon","France"),("Nice","France"),
            ("Berlin","Germany"),("Munich","Germany"),("Hamburg","Germany"),
            ("Vienna","Austria"),("Salzburg","Austria"),
            ("Rome","Italy"),("Milan","Italy"),("Florence","Italy"),
            ("Barcelona","Spain"),("Madrid","Spain"),("Valencia","Spain"),
            ("Amsterdam","Netherlands"),("Rotterdam","Netherlands"),
            ("London","United Kingdom"),("Edinburgh","United Kingdom"),
            ("Prague","Czech Republic"),("Budapest","Hungary"),
            ("Lisbon","Portugal"),("Porto","Portugal"),
            ("New York","USA"),("Los Angeles","USA"),("Chicago","USA"),
            ("Tokyo","Japan"),("Osaka","Japan"),
            ("Bangkok","Thailand"),("Singapore","Singapore"),
            ("Dubai","UAE"),("Istanbul","Turkey"),
            ("Copenhagen","Denmark"),("Stockholm","Sweden"),("Oslo","Norway")
        };

        // -----------------------------
        // GROUPS (20)
        // -----------------------------
        var now = DateTime.UtcNow;

        var groups = Enumerable.Range(1, 20)
            .Select(i =>
            {
                var city = cities[rng.Next(cities.Length)];
                return new Group
                {
                    Id = Guid.NewGuid(),
                    Name = $"Trip Group {i}",
                    Description = "Seeded travel group",
                    DestinationCityName = city.City,
                    DestinationCountry = city.Country,
                    CreatedAt = now,
                    ModifiedAt = now
                };
            })
            .ToList();

        db.Groups.AddRange(groups);
        await db.SaveChangesAsync(ct);

        // -----------------------------
        // MEMBERS (5..8 per group)
        // -----------------------------
        var members = new List<GroupMember>();

        foreach (var group in groups)
        {
            var groupSize = rng.Next(5, 9); // 5..8
            var selectedUsers = users
                .OrderBy(_ => rng.Next())
                .Take(groupSize)
                .ToList();

            for (int i = 0; i < selectedUsers.Count; i++)
            {
                members.Add(new GroupMember
                {
                    GroupId = group.Id,
                    UserId = selectedUsers[i].Id,
                    Role = i == 0 ? GroupRole.Admin : GroupRole.Contributor,
                    JoinedAt = now.AddDays(-rng.Next(0, 60))
                });
            }
        }

        db.GroupMembers.AddRange(members);
        await db.SaveChangesAsync(ct);

        // -----------------------------
        // ITEMS (20..30 per group, CHF only)
        // -----------------------------
        var items = new List<Item>();

        foreach (var group in groups)
        {
            var groupMembers = members.Where(m => m.GroupId == group.Id).ToList();
            var itemCount = rng.Next(20, 31); // 20..30

            for (int i = 0; i < itemCount; i++)
            {
                var payer = groupMembers[rng.Next(groupMembers.Count)];

                // payees: at least 1, up to groupMembers.Count (but not too many)
                var maxPayees = Math.Min(groupMembers.Count, 6);
                var payeeCount = rng.Next(1, maxPayees + 1);

                var payees = groupMembers
                    .OrderBy(_ => rng.Next())
                    .Take(payeeCount)
                    .Select(m => m.UserId)
                    .ToList();

                // payer muss NICHT automatisch payee sein (wie du es wolltest)
                items.Add(new Item
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    PaidByMemberId = payer.UserId,
                    Title = $"Expense {i + 1}",
                    Description = rng.NextDouble() < 0.35 ? "Seeded expense details" : "", // etwas variieren
                    Amount = Math.Round((decimal)(rng.NextDouble() * 200 + 5), 2), // 5..205
                    Currency = "CHF",
                    CreatedAt = now.AddDays(-rng.Next(0, 30)),
                    PayeeUserIds = payees
                });
            }
        }

        db.Items.AddRange(items);
        await db.SaveChangesAsync(ct);

        // -----------------------------
        // VOTING SESSIONS + CANDIDATES + VOTES (optional / kleiner)
        // -----------------------------
        // Wenn du’s noch stärker entschlacken willst: diesen Block einfach löschen.
        var sessions = new List<VotingSession>();
        var candidates = new List<Candidate>();
        var votes = new List<Vote>();

        foreach (var group in groups)
        {
            var session = new VotingSession
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                Status = VotingStatus.Closed,
                MaxVotesPerMember = rng.Next(2, 5),
                CreatedAt = now.AddDays(-rng.Next(5, 25)),
                ClosedAt = now.AddDays(-rng.Next(1, 5))
            };

            sessions.Add(session);

            var candidateCities = cities.OrderBy(_ => rng.Next()).Take(5).ToList();
            var groupMembers = members.Where(m => m.GroupId == group.Id).ToList();

            var sessionCandidates = candidateCities.Select(city => new Candidate
            {
                Id = Guid.NewGuid(),
                VotingSessionId = session.Id,
                CityName = city.City,
                Country = city.Country,
                CreatedByUserId = groupMembers[rng.Next(groupMembers.Count)].UserId,
                CreatedAt = session.CreatedAt.AddMinutes(rng.Next(1, 120))
            }).ToList();

            candidates.AddRange(sessionCandidates);

            foreach (var member in groupMembers)
            {
                var voteCount = rng.Next(1, session.MaxVotesPerMember + 1);
                for (int v = 0; v < voteCount; v++)
                {
                    var cand = sessionCandidates[rng.Next(sessionCandidates.Count)];
                    votes.Add(new Vote
                    {
                        Id = Guid.NewGuid(),
                        VotingSessionId = session.Id,
                        CandidateId = cand.Id,
                        UserId = member.UserId,
                        CreatedAt = session.CreatedAt.AddMinutes(rng.Next(1, 240))
                    });
                }
            }

            var winner = votes
                .Where(v => v.VotingSessionId == session.Id)
                .GroupBy(v => v.CandidateId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            var winningCandidate = sessionCandidates.FirstOrDefault(c => c.Id == winner);
            if (winningCandidate != null)
            {
                group.DestinationCityName = winningCandidate.CityName;
                group.DestinationCountry = winningCandidate.Country;
            }
        }

        db.VotingSessions.AddRange(sessions);
        db.Candidates.AddRange(candidates);
        db.Votes.AddRange(votes);

        await db.SaveChangesAsync(ct);
    }
}