namespace QuestceSpire.Core
{
    public static class ArchetypeUtils
    {
        public static bool MatchesCoreTag(Archetype archetype, string tag)
        {
            return archetype.CoreTags.Contains(tag);
        }

        public static bool MatchesSupportTag(Archetype archetype, string tag)
        {
            return archetype.SupportTags != null && archetype.SupportTags.Contains(tag);
        }

        public static bool MatchesAnyTag(Archetype archetype, string tag)
        {
            return MatchesCoreTag(archetype, tag) || MatchesSupportTag(archetype, tag);
        }
    }
}
