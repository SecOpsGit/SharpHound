﻿using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Newtonsoft.Json;
using Ingestor.Enumeration;

namespace Ingestor
{
    public static class Extensions
    {
        private static readonly HashSet<string> Groups = new HashSet<string> { "268435456", "268435457", "536870912", "536870913" };
        private static readonly HashSet<string> Computers = new HashSet<string> { "805306369" };
        private static readonly HashSet<string> Users = new HashSet<string> { "805306368" };
        //private static readonly Regex SpnSearch = new Regex(@"HOST\/([A-Za-z0-9-_]*\.[A-Za-z0-9-_]*\.[A-Za-z0-9-_]*)$", RegexOptions.Compiled);
        private static string _primaryDomain;

        internal static void SetPrimaryDomain(string domain)
        {
            _primaryDomain = domain;
        }

        public static string ToTitleCase(this string str)
        {
            return str.Substring(0, 1).ToUpper() + str.Substring(1).ToLower();
        }

        //Helper method for testing stuff
        internal static void PrintEntry(this SearchResultEntry result)
        {
            foreach (var property in result.Attributes.AttributeNames)
            {
                Console.WriteLine(property);
            }
        }

        public static bool HasFlag(this Enum self, Enum test)
        {
            if (self == null || test == null)
            {
                return false;
            }

            try
            {
                var temp = Convert.ToInt32(self);
                var num = Convert.ToInt32(test);
                return (temp & num) == num;
            }
            catch (Exception)
            {
                return false;
            }
            
        }

        internal static void CloseC(this JsonTextWriter writer, int count, string type)
        {
            writer.Flush();
            writer.WriteEndArray();
            writer.WritePropertyName("meta");
            writer.WriteStartObject();
            writer.WritePropertyName("count");
            writer.WriteValue(count);
            writer.WritePropertyName("type");
            writer.WriteValue(type);
            writer.WriteEndObject();
            writer.Close();
        }

        internal static ResolvedEntry ResolveAdEntry(this SearchResultEntry result, bool bypassDns = false)
        {
            var entry = new ResolvedEntry();

            var accountName = result.GetProp("samaccountname");
            var distinguishedName = result.DistinguishedName;
            var accountType = result.GetProp("samaccounttype");

            if (distinguishedName == null)
                return null;

            var domainName = Utils.ConvertDnToDomain(distinguishedName);

            if (MappedPrincipal.GetCommon(result.GetSid(), out var temp))
            {
                return new ResolvedEntry
                {
                    IngestCacheDisplay = $"{temp.PrincipalName}@{domainName}".ToUpper(),
                    ObjectType = temp.ObjectType
                };
            }
            
            if (Groups.Contains(accountType))
            {
                entry.IngestCacheDisplay = $"{accountName}@{domainName}".ToUpper();
                entry.ObjectType = "group";
                return entry;
            }

            if (Users.Contains(accountType))
            {
                entry.IngestCacheDisplay = $"{accountName}@{domainName}".ToUpper();
                entry.ObjectType = "user";
                return entry;
            }

            if (Computers.Contains(accountType))
            {
                var shortName = accountName?.TrimEnd('$');
                var dnshostname = result.GetProp("dnshostname");
                if (dnshostname == null)
                {
                    var domain = Utils.ConvertDnToDomain(result.DistinguishedName);
                    dnshostname = $"{shortName}.{domain}".ToUpper();
                }

                entry.IngestCacheDisplay = dnshostname;
                entry.ObjectType = "computer";
                entry.ComputerSamAccountName = shortName;
                return entry;
            }
            
            if (accountType == null)
            {
                var objClass = result.GetPropArray("objectClass");
                if (objClass.Contains("groupPolicyContainer"))
                {
                    entry.IngestCacheDisplay = $"{result.GetProp("displayname")}@{domainName}";
                    entry.ObjectType = "gpo";
                    return entry;
                }

                if (objClass.Contains("organizationalUnit"))
                {
                    entry.IngestCacheDisplay = $"{result.GetProp("name")}@{domainName}";
                    entry.ObjectType = "ou";
                    return entry;
                }

                if (objClass.Contains("container"))
                {
                    entry.IngestCacheDisplay = domainName;
                    entry.ObjectType = "container";
                    return entry;
                }
            }
            else
            {
                if (accountType.Equals("805306370"))
                    return null;
            }
            entry.IngestCacheDisplay = domainName;
            entry.ObjectType = "domain";
            return entry;
        }

        public static string GetProp(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return null;

            return result.Attributes[prop][0].ToString();
        }

        public static byte[] GetPropBytes(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return null;

            return result.Attributes[prop][0] as byte[];
        }

        public static string[] GetPropArray(this SearchResultEntry result, string prop)
        {
            if (!result.Attributes.Contains(prop))
                return new string[0];

            var values = result.Attributes[prop];

            var toreturn = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
                toreturn[i] = values[i].ToString();

            return toreturn;
        }

        
        public static string GetSid(this SearchResultEntry result)
        {
            if (!result.Attributes.Contains("objectsid"))
                return null;

            var s = result.Attributes["objectsid"][0];
            switch (s)
            {
                case byte[] b:
                    return new SecurityIdentifier(b, 0).ToString();
                case string st:
                    return new SecurityIdentifier(Encoding.ASCII.GetBytes(st), 0).ToString();
            }

            return null;
        }

        public static string GetSid(this DirectoryEntry result)
        {
            if (!result.Properties.Contains("objectsid"))
                return null;

            var s = result.Properties["objectsid"][0];
            switch (s)
            {
                case byte[] b:
                    return new SecurityIdentifier(b, 0).ToString();
                case string st:
                    return new SecurityIdentifier(Encoding.ASCII.GetBytes(st), 0).ToString();
            }

            return null;
        }
    }
}
