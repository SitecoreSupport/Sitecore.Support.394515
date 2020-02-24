

using GraphQL.Types;
namespace Sitecore.Support.Services.GraphQL.Content.Queries
{

    
    using Sitecore;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Linq;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using Sitecore.Globalization;
    using Sitecore.Services.GraphQL.Content;
    using Sitecore.Services.GraphQL.Content.GraphTypes;
    using Sitecore.Services.GraphQL.Content.GraphTypes.ContentSearch;
    using Sitecore.Services.GraphQL.Content.Queries;
    using Sitecore.Services.GraphQL.GraphTypes.Connections;
    using Sitecore.Services.GraphQL.Schemas;
    using System.Collections.Generic;
    using Sitecore.Diagnostics;
    using System.Linq;


    public class SearchQuery : RootFieldType<ContentSearchResultsGraphType, ContentSearchResults>, IContentSchemaRootFieldType
    {
        protected class ItemSearchFieldQueryValueGraphType : InputObjectGraphType
        {
            public ItemSearchFieldQueryValueGraphType()
            {
                base.Name = "ItemSearchFieldQuery";
                Field<NonNullGraphType<StringGraphType>>("name", "Index field name to filter on");
                Field<NonNullGraphType<StringGraphType>>("value", "Field value to filter on");
            }
        }

        public Database Database
        {
            get;
            set;
        }

        public SearchQuery()
            : base("search", "Allows querying the Content Search indexes")
        {
            QueryArguments queryArguments = new QueryArguments();
            queryArguments.AddConnectionArguments();
            queryArguments.Add(new QueryArgument<StringGraphType>
            {
                Name = "rootItem",
                Description = "ID or path of an item to search under (results will be descendants)"
            });
            queryArguments.Add(new QueryArgument<StringGraphType>
            {
                Name = "keyword",
                Description = "Search by keyword (default: no keyword search)"
            });
            queryArguments.Add(new QueryArgument<StringGraphType>
            {
                Name = "language",
                Description = "The item language to request (defaults to the context language)"
            });
            queryArguments.Add(new QueryArgument<BooleanGraphType>
            {
                Name = "latestVersion",
                Description = "The item version to request (if not set, latest version is returned)",
                DefaultValue = true
            });
            queryArguments.Add(new QueryArgument<StringGraphType>
            {
                Name = "index",
                Description = "The search index name to query (defaults to the standard index for the current database)"
            });
            queryArguments.Add(new QueryArgument<ListGraphType<ItemSearchFieldQueryValueGraphType>>
            {
                Name = "fieldsEqual",
                Description = "Filter by index field value using equality (multiple fields are ANDed)"
            });
            queryArguments.Add(new QueryArgument<ListGraphType<NonNullGraphType<StringGraphType>>>
            {
                Name = "facetOn",
                Description = "Index field names to facet results on"
            });
            base.Arguments = queryArguments;
        }

        protected override ContentSearchResults Resolve(ResolveFieldContext context)
        {
            string argument = context.GetArgument<string>("rootItem");
            ID rootId = null;
            Item result2;
            if (!string.IsNullOrWhiteSpace(argument) && IdHelper.TryResolveItem(Database, argument, out result2))
            {
                rootId = result2.ID;
            }
            string keywordArg = context.GetArgument<string>("keyword");
            string name2 = context.GetArgument<string>("language") ?? Context.Language.Name ?? LanguageManager.DefaultLanguage.Name;
            Language result3;
            if (!Language.TryParse(name2, out result3))
            {
                result3 = null;
            }
            bool flag = context.GetArgument<bool?>("version") ?? true;
            string name3 = context.GetArgument<string>("index") ?? string.Format("sitecore_{0}_index", Database.Name.ToLowerInvariant());
            object[] source = context.GetArgument("fieldsEqual", new object[0]);// This is the patch fix which is learned from JSS 13.0.0 code 
            IEnumerable<Dictionary<string, object>> enumerable = source.OfType<Dictionary<string, object>>();
            IEnumerable<string> enumerable2 = context.GetArgument<IEnumerable<string>>("facetOn") ?? new string[0];
            using (IProviderSearchContext providerSearchContext = ContentSearchManager.GetIndex(name3).CreateSearchContext())
            {
                IQueryable<ContentSearchResult> queryable = providerSearchContext.GetQueryable<ContentSearchResult>();
                if (rootId != (ID)null)
                {
                    queryable = queryable.Where((ContentSearchResult result) => result.AncestorIDs.Contains(rootId));
                }
                if (!string.IsNullOrWhiteSpace(keywordArg))
                {
                    queryable = queryable.Where((ContentSearchResult result) => result.Content.Contains(keywordArg));
                }
                if (result3 != null)
                {
                    string resultLanguage = result3.Name;
                    queryable = queryable.Where((ContentSearchResult result) => result.Language == resultLanguage);
                }
                if (flag)
                {
                    queryable = queryable.Where((ContentSearchResult result) => result.IsLatestVersion);
                }
                foreach (Dictionary<string, object> item in enumerable)
                {
                    string name = item["name"].ToString();
                    string value = item["value"].ToString();
                    queryable = queryable.Where((ContentSearchResult result) => result[name].Equals(value));
                }
                foreach (string facet in enumerable2)
                {
                    queryable = queryable.FacetOn((ContentSearchResult result) => result[facet]);
                }
                int? argument2 = context.GetArgument<int?>("after");
                queryable = queryable.ApplyEnumerableConnectionArguments(context);
                return new ContentSearchResults(queryable.GetResults(), argument2 ?? 0);
            }
        }
    }
}
