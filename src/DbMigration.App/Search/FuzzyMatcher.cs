namespace DbMigration.App.Search;

internal static class FuzzyMatcher
{
    public static int Score(string query, string candidate)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }

        if (string.IsNullOrEmpty(candidate))
        {
            return -1;
        }

        int qi = 0;
        int score = 0;
        int run = 0;
        char previous = '\0';

        for (int ci = 0; ci < candidate.Length && qi < query.Length; ci++)
        {
            char c = char.ToLowerInvariant(candidate[ci]);
            char q = char.ToLowerInvariant(query[qi]);

            if (c == q)
            {
                bool wordBoundary = ci == 0 || IsSeparator(previous) || char.IsUpper(candidate[ci]) && char.IsLower(previous);
                run = wordBoundary ? 1 : run + 1;
                score += 10 + run * 6;

                if (ci == 0)
                {
                    score += 8;
                }

                qi++;
            }
            else
            {
                run = 0;
                score -= 1;
            }

            previous = candidate[ci];
        }

        if (qi < query.Length)
        {
            return -1;
        }

        score -= candidate.Length;
        return score;
    }

    public static bool IsMatch(string query, string candidate) => Score(query, candidate) >= 0;

    private static bool IsSeparator(char c) =>
        c is '_' or '-' or '.' or ' ' or '/' or '\\' or ':' or '>';
}

