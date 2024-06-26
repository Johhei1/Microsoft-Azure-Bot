using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

public class SearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly SearchIndexerClient _indexerClient;
    private readonly IConfiguration _configuration;

    public SearchService(IConfiguration configuration)
    {
        _configuration = configuration;
        var apiKey = configuration["AzureSearch:AdminKey"];

        var serviceEndpoint = new Uri(configuration["AzureSearch:ServiceEndpoint"]);
        var indexName = configuration["AzureSearch:IndexName"];

        _indexClient = new SearchIndexClient(serviceEndpoint, new AzureKeyCredential(apiKey));
        _indexerClient = new SearchIndexerClient(serviceEndpoint, new AzureKeyCredential(apiKey));
        _searchClient = _indexClient.GetSearchClient(indexName);
    }

    public async Task CreateOrUpdateIndexAsync()
    {
        var index = GetSearchIndex();
        await _indexClient.CreateOrUpdateIndexAsync(index);
    }

    public async Task ResetIndexerAsync(string indexerName)
    {
        await _indexerClient.ResetIndexerAsync(indexerName);
    }

    public async Task CreateOrUpdateIndexerAsync()
    {
        var dataSource = new SearchIndexerDataSourceConnection(
            name: $"{_configuration["AzureSearch:IndexName"]}-blob",
            type: SearchIndexerDataSourceType.AzureBlob,
            connectionString: _configuration["AzureBlobStorage:ConnectionString"],
            container: new SearchIndexerDataContainer(_configuration["AzureBlobStorage:ContainerName"])
        );

        await _indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSource);

        var indexerName = $"{_configuration["AzureSearch:IndexName"]}-indexer";

        try
        {
            // Try to reset the indexer if it exists
            await ResetIndexerAsync(indexerName);
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine($"Error resetting indexer: {ex.Message}");
        }

        var indexer = new SearchIndexer(
            name: indexerName,
            dataSourceName: dataSource.Name,
            targetIndexName: _configuration["AzureSearch:IndexName"])
        {
            Schedule = new IndexingSchedule(TimeSpan.FromDays(1)), // Example schedule, adjust as needed
            Parameters = new IndexingParameters
            {
                BatchSize = 10,
                MaxFailedItems = 10,
                MaxFailedItemsPerBatch = 5
            }
        };

        await _indexerClient.CreateOrUpdateIndexerAsync(indexer);
        await _indexerClient.RunIndexerAsync(indexer.Name);
    }

    public async Task<SearchResults<SearchDocument>> SearchAsync(string query, int k = 3, string filter = null)
    {
        var searchOptions = new SearchOptions
        {
            Filter = filter,
            Size = k,
            IncludeTotalCount = true,
            Select = { "content", "title" }
        };

        return await _searchClient.SearchAsync<SearchDocument>(query, searchOptions);
    }

    private SearchIndex GetSearchIndex()
    {
        var indexName = _configuration["AzureSearch:IndexName"];

        return new SearchIndex(indexName)
        {
            Fields =
            {
                new SearchableField("content"),
                new SearchableField("title"),
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true },
                new SimpleField("filepath", SearchFieldDataType.String),
                new SimpleField("url", SearchFieldDataType.String),
                new SimpleField("chunk_id", SearchFieldDataType.String),
                new SimpleField("last_updated", SearchFieldDataType.String)
            }
        };
    }
}
