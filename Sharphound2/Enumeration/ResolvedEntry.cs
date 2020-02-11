namespace Ingestor.Enumeration
{
    internal class ResolvedEntry
    {
        private string _displayName;
        private string _objecttype;

        public string IngestCacheDisplay
        {
            get => _displayName.ToUpper();
            set => _displayName = value;
        }
        public string ObjectType
        {
            get => _objecttype.ToLower();
            set => _objecttype = value;
        }

        public string ComputerSamAccountName { get; set; }

        public override string ToString()
        {
            return $"{IngestCacheDisplay} - {ObjectType}";
        }
    }
}
