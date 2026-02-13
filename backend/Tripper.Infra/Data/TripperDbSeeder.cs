using Microsoft.EntityFrameworkCore;
using Tripper.Core.Entities;
using Tripper.Core.Interfaces;

namespace Tripper.Infra.Data;

public static class TripperDbSeeder
{
    // Deterministic seed so your data is reproducible between runs
    private const int Seed = 42;

    public static async Task SeedAsync(
        TripperDbContext db,
        IPasswordHasher passwordHasher,
        CancellationToken ct = default)
    {
        // Ensure DB exists & is migrated
        await db.Database.MigrateAsync(ct);

        // One-time seed guard
        if (await db.Users.AnyAsync(ct))
            return;

        var rng = new Random(Seed);
        var now = DateTime.UtcNow;

        // Real cities + countries (curated list, no web call)
        var cities = CityData.All;

        // --- USERS (250) ---
        var users = new List<User>(capacity: 250);
        for (var i = 1; i <= 250; i++)
        {
            var username = $"user{i:000}";
            var email = $"{username}@tripper.local";
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = passwordHasher.HashPassword("Password123!"),
                CreatedAt = now.AddDays(-rng.Next(0, 365))
            });
        }

        db.Users.AddRange(users);
        await db.SaveChangesAsync(ct);

        // --- GROUPS (50) + MEMBERS (50..80) + ITEMS (100..150) + CLOSED VOTING ---
        var groups = new List<Group>(capacity: 50);
        var groupMembers = new List<GroupMember>(capacity: 50 * 70);
        var items = new List<Item>(capacity: 50 * 125);
        var votingSessions = new List<VotingSession>(capacity: 50);
        var candidates = new List<Candidate>(capacity: 50 * 10);
        var votes = new List<Vote>(capacity: 50 * 70);

        var currencies = new[] { "CHF", "EUR", "USD", "GBP", "JPY" };
        var itemTitles = new[]
        {
            "Hotel", "Dinner", "Groceries", "Museum Tickets", "Train", "Taxi",
            "Flight", "Coffee", "Drinks", "Souvenirs", "SIM Card", "Parking",
            "Guided Tour", "Event Tickets", "Car Rental"
        };

        for (var g = 1; g <= 50; g++)
        {
            var groupId = Guid.NewGuid();

            // Group meta (destination will be set after voting)
            var group = new Group
            {
                Id = groupId,
                Name = $"Tripper Group {g:00}",
                Description = $"Seeded demo group #{g:00} for Tripper MVP.",
                CreatedAt = now.AddDays(-rng.Next(0, 180)),
                ModifiedAt = now.AddDays(-rng.Next(0, 30))
            };
            groups.Add(group);

            // Members 50..80
            var memberCount = rng.Next(50, 81);
            var memberUsers = PickDistinctUsers(rng, users, memberCount);

            // Admins 2..5
            var adminCount = rng.Next(2, 6);
            var adminUsers = memberUsers.OrderBy(_ => rng.Next()).Take(adminCount).ToHashSet();

            groupMembers.AddRange(
                memberUsers.Select(u => new GroupMember
                    {
                        GroupId = groupId, 
                        UserId = u.Id, 
                        Role = adminUsers.Contains(u) ? GroupRole.Admin : GroupRole.Contributor, 
                        JoinedAt = group.CreatedAt.AddDays(rng.Next(0, 30))
                    }));

            // Items 100..150
            var itemCount = rng.Next(100, 151);
            for (var i = 0; i < itemCount; i++)
            {
                var paidBy = memberUsers[rng.Next(memberUsers.Count)];
                var payeeCount = rng.Next(2, Math.Min(11, memberUsers.Count + 1)); // 2..10
                var payees = memberUsers
                    .OrderBy(_ => rng.Next())
                    .Take(payeeCount)
                    .Select(u => u.Id)
                    .Distinct()
                    .ToList();

                // Ensure paidBy is usually among payees (optional, but realistic)
                if (!payees.Contains(paidBy.Id) && rng.NextDouble() < 0.75)
                    payees.Add(paidBy.Id);

                var createdAt = group.CreatedAt.AddDays(rng.Next(0, 30)).AddMinutes(rng.Next(0, 1440));

                items.Add(new Item
                {
                    Id = Guid.NewGuid(),
                    GroupId = groupId,
                    PaidByMemberId = paidBy.Id,   // NOTE: this is a UserId in your model
                    Amount = Math.Round((decimal)(rng.NextDouble() * 250.0 + 5.0), 2),
                    Currency = currencies[rng.Next(currencies.Length)],
                    Title = itemTitles[rng.Next(itemTitles.Length)],
                    Description = "Seeded expense item (demo data).",
                    CreatedAt = createdAt,
                    PayeeUserIds = payees
                });
            }

            // Voting session (one per group, already closed)
            var votingId = Guid.NewGuid();
            const int maxVotes = 1; // keep it simple: every member votes once (matches your requirement nicely)

            var vs = new VotingSession
            {
                Id = votingId,
                GroupId = groupId,
                Status = VotingStatus.Closed,
                MaxVotesPerMember = maxVotes,
                CreatedAt = group.CreatedAt.AddDays(rng.Next(0, 7)),
                ClosedAt = group.CreatedAt.AddDays(rng.Next(7, 21))
            };
            votingSessions.Add(vs);

            // Candidates 6..12 distinct cities
            var candidateCount = rng.Next(6, 13);
            var candidateCities = cities.OrderBy(_ => rng.Next()).Take(candidateCount).ToList();

            // Pick a winner and bias votes toward it
            var winnerIndex = rng.Next(candidateCities.Count);
            var winner = candidateCities[winnerIndex];

            var candidateIds = new List<Guid>(candidateCount);
            for (var c = 0; c < candidateCount; c++)
            {
                var createdBy = memberUsers[rng.Next(memberUsers.Count)];
                var (city, country) = candidateCities[c];

                var cid = Guid.NewGuid();
                candidateIds.Add(cid);

                candidates.Add(new Candidate
                {
                    Id = cid,
                    VotingSessionId = votingId,
                    CityName = city,
                    Country = country,
                    CreatedByUserId = createdBy.Id,
                    CreatedAt = vs.CreatedAt.AddMinutes(rng.Next(0, 600))
                });
            }

            // Votes: every member votes once; 60% chance for winner, else random among others
            var winnerCandidateId = candidateIds[winnerIndex];

            foreach (var u in memberUsers)
            {
                var pickWinner = rng.NextDouble() < 0.60;
                Guid chosenCandidateId;

                if (pickWinner)
                {
                    chosenCandidateId = winnerCandidateId;
                }
                else
                {
                    // choose among non-winner
                    var alt = candidateIds.Where(id => id != winnerCandidateId).ToList();
                    chosenCandidateId = alt[rng.Next(alt.Count)];
                }

                votes.Add(new Vote
                {
                    Id = Guid.NewGuid(),
                    VotingSessionId = votingId,
                    CandidateId = chosenCandidateId,
                    UserId = u.Id,
                    CreatedAt = vs.CreatedAt.AddMinutes(rng.Next(0, 1440))
                });
            }

            // Set group destination to the winning city/country
            group.DestinationCityName = winner.city;
            group.DestinationCountry = winner.country;
            group.ModifiedAt = vs.ClosedAt ?? group.ModifiedAt;
        }

        db.Groups.AddRange(groups);
        db.GroupMembers.AddRange(groupMembers);
        db.Items.AddRange(items);
        db.VotingSessions.AddRange(votingSessions);
        db.Candidates.AddRange(candidates);
        db.Votes.AddRange(votes);

        await db.SaveChangesAsync(ct);
        return;

        // Helper: pick N distinct users from pool
        static List<User> PickDistinctUsers(Random rng, List<User> pool, int n)
        {
            // Fisher-Yates partial shuffle via indices
            var indices = Enumerable.Range(0, pool.Count).ToArray();
            for (var i = 0; i < n; i++)
            {
                var j = rng.Next(i, indices.Length);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }
            var result = new List<User>(n);
            for (var i = 0; i < n; i++) result.Add(pool[indices[i]]);
            return result;
        }
    }

    private static class CityData
    {
        public static readonly List<(string city, string country)> All =
        [
            ("Zurich", "Switzerland"), ("Geneva", "Switzerland"), ("Bern", "Switzerland"), ("Basel", "Switzerland"),
            ("Paris", "France"), ("Lyon", "France"), ("Marseille", "France"), ("Nice", "France"),
            ("Berlin", "Germany"), ("Munich", "Germany"), ("Hamburg", "Germany"), ("Cologne", "Germany"),
            ("Vienna", "Austria"), ("Salzburg", "Austria"), ("Graz", "Austria"),
            ("Rome", "Italy"), ("Milan", "Italy"), ("Florence", "Italy"), ("Venice", "Italy"), ("Naples", "Italy"),
            ("Barcelona", "Spain"), ("Madrid", "Spain"), ("Valencia", "Spain"), ("Seville", "Spain"),
            ("Lisbon", "Portugal"), ("Porto", "Portugal"),
            ("Amsterdam", "Netherlands"), ("Rotterdam", "Netherlands"), ("Utrecht", "Netherlands"),
            ("Brussels", "Belgium"), ("Antwerp", "Belgium"), ("Ghent", "Belgium"),
            ("London", "United Kingdom"), ("Edinburgh", "United Kingdom"), ("Manchester", "United Kingdom"),
            ("Glasgow", "United Kingdom"),
            ("Dublin", "Ireland"), ("Cork", "Ireland"),
            ("Copenhagen", "Denmark"), ("Aarhus", "Denmark"),
            ("Stockholm", "Sweden"), ("Gothenburg", "Sweden"), ("Malmo", "Sweden"),
            ("Oslo", "Norway"), ("Bergen", "Norway"),
            ("Helsinki", "Finland"), ("Turku", "Finland"),
            ("Prague", "Czechia"), ("Brno", "Czechia"),
            ("Warsaw", "Poland"), ("Krakow", "Poland"), ("Gdansk", "Poland"), ("Wroclaw", "Poland"),
            ("Budapest", "Hungary"),
            ("Athens", "Greece"), ("Thessaloniki", "Greece"),
            ("Istanbul", "Turkey"), ("Ankara", "Turkey"),
            ("Reykjavik", "Iceland"),
            ("New York", "United States"), ("San Francisco", "United States"), ("Chicago", "United States"),
            ("Boston", "United States"),
            ("Toronto", "Canada"), ("Vancouver", "Canada"), ("Montreal", "Canada"),
            ("Mexico City", "Mexico"),
            ("Rio de Janeiro", "Brazil"), ("Sao Paulo", "Brazil"),
            ("Buenos Aires", "Argentina"),
            ("Santiago", "Chile"),
            ("Bogota", "Colombia"),
            ("Tokyo", "Japan"), ("Kyoto", "Japan"), ("Osaka", "Japan"), ("Sapporo", "Japan"),
            ("Seoul", "South Korea"), ("Busan", "South Korea"),
            ("Beijing", "China"), ("Shanghai", "China"), ("Shenzhen", "China"), ("Hong Kong", "China"),
            ("Singapore", "Singapore"),
            ("Bangkok", "Thailand"), ("Chiang Mai", "Thailand"),
            ("Hanoi", "Vietnam"), ("Ho Chi Minh City", "Vietnam"),
            ("Kuala Lumpur", "Malaysia"),
            ("Jakarta", "Indonesia"), ("Bali (Denpasar)", "Indonesia"),
            ("Sydney", "Australia"), ("Melbourne", "Australia"), ("Brisbane", "Australia"),
            ("Auckland", "New Zealand"),
            ("Dubai", "United Arab Emirates"), ("Abu Dhabi", "United Arab Emirates"),
            ("Tel Aviv", "Israel"),
            ("Cairo", "Egypt"),
            ("Marrakesh", "Morocco"), ("Casablanca", "Morocco"),
            ("Cape Town", "South Africa"), ("Johannesburg", "South Africa")
        ];
    }
}