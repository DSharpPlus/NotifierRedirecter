using DSharpPlus;
using DSharpPlus.EventArgs;

namespace NotifierRedirecter
{
    internal static class Events
    {
        public static Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
        {
            if (e.Channel.Id != Program.JsonConfig.RedirectionChannel)
            {
                return Task.CompletedTask;
            }


            ReadOnlySpan<char> span = e.Message.Content.AsSpan();
            if (span.IsWhiteSpace())
            {
                return Task.CompletedTask;
            }

            int firstAppearence = span.IndexOf('<');
            if (firstAppearence == -1)
            {
                return Task.CompletedTask;
            }
            if (firstAppearence != 0)
            {
                if (span[firstAppearence - 1] == '\\')
                {
                    return Task.CompletedTask;
                }
            }

            int lastAppearence = span.IndexOf('>');
            if (lastAppearence == -1)
            {
                return Task.CompletedTask;
            }

            ReadOnlySpan<char> mention = span[firstAppearence..(lastAppearence + 1)];
            Console.WriteLine(mention.ToString());
            if (mention is not ['<', '@', .., '>'])
            {
                return Task.CompletedTask;
            }

            ReadOnlySpan<char> userIdSpan = mention[2..^1];
            if (ulong.TryParse(userIdSpan, out ulong userId))
            {
                if (userId == e.Author.Id)
                {
                    return Task.CompletedTask;
                }

                _ = Utils.RedirectMention(client, e, userId);
            }

            return Task.CompletedTask;
        }
    }
}