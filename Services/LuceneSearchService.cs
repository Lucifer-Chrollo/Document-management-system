using DocumentManagementSystem.Models;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;

namespace DocumentManagementSystem.Services;

public class LuceneSearchService : ISearchService, IDisposable
{
    private readonly string _indexPath;
    private readonly LuceneVersion _version = LuceneVersion.LUCENE_48;
    private readonly StandardAnalyzer _analyzer;
    private readonly FSDirectory _directory;
    private readonly ILogger<LuceneSearchService> _logger;

    public LuceneSearchService(IConfiguration configuration, ILogger<LuceneSearchService> logger)
    {
        _logger = logger;
        
        // Store index in App_Data/LuceneIndex by default
        var storagePath = configuration["Storage:LocalPath"] 
            ?? Path.Combine(System.IO.Directory.GetCurrentDirectory(), "DocumentStorage");
        _indexPath = Path.Combine(Path.GetDirectoryName(storagePath)!, "LuceneIndex");
        
        if (!System.IO.Directory.Exists(_indexPath))
        {
            System.IO.Directory.CreateDirectory(_indexPath);
        }

        _analyzer = new StandardAnalyzer(_version);
        _directory = FSDirectory.Open(_indexPath);
    }

    public void IndexDocument(Document document)
    {
        try
        {
            using var writer = new IndexWriter(_directory, new IndexWriterConfig(_version, _analyzer));
            var doc = new Lucene.Net.Documents.Document();

            // Store ID to retrieve it later
            doc.Add(new Lucene.Net.Documents.StringField("Id", document.DocumentId.ToString(), Lucene.Net.Documents.Field.Store.YES));

            // Index fields for searching
            if (!string.IsNullOrEmpty(document.DocumentName))
                doc.Add(new Lucene.Net.Documents.TextField("Name", document.DocumentName, Lucene.Net.Documents.Field.Store.YES));
            
            if (!string.IsNullOrEmpty(document.BatchLabel))
                doc.Add(new Lucene.Net.Documents.TextField("BatchLabel", document.BatchLabel, Lucene.Net.Documents.Field.Store.YES));
            
            if (!string.IsNullOrEmpty(document.CategoryName))
                doc.Add(new Lucene.Net.Documents.TextField("Category", document.CategoryName, Lucene.Net.Documents.Field.Store.YES));
                
            if (!string.IsNullOrEmpty(document.OcrText))
                doc.Add(new Lucene.Net.Documents.TextField("Content", document.OcrText, Lucene.Net.Documents.Field.Store.NO));

            // Update (delete old & insert new) based on Term ID
            writer.UpdateDocument(new Term("Id", document.DocumentId.ToString()), doc);
            writer.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {Id}", document.DocumentId);
        }
    }

    public Task IndexDocumentAsync(Document document)
    {
        return Task.Run(() => IndexDocument(document));
    }

    public void DeleteDocument(int documentId)
    {
        try
        {
            using var writer = new IndexWriter(_directory, new IndexWriterConfig(_version, _analyzer));
            writer.DeleteDocuments(new Term("Id", documentId.ToString()));
            writer.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {Id} from index", documentId);
        }
    }

    public Task DeleteDocumentAsync(int documentId)
    {
        return Task.Run(() => DeleteDocument(documentId));
    }

    public IEnumerable<int> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<int>();

        try
        {
            if (!DirectoryReader.IndexExists(_directory)) return Enumerable.Empty<int>();

            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);

            // Search across multiple fields
            var fields = new[] { "Name", "BatchLabel", "Category", "Content" };
            var parser = new MultiFieldQueryParser(_version, fields, _analyzer);
            var luceneQuery = parser.Parse(query);

            var hits = searcher.Search(luceneQuery, 100).ScoreDocs; // Top 100 results

            var ids = new List<int>();
            foreach (var hit in hits)
            {
                var foundDoc = searcher.Doc(hit.Doc);
                if (int.TryParse(foundDoc.Get("Id"), out int id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Lucene search for {Query}", query);
            return Enumerable.Empty<int>();
        }
    }

    public Task<IEnumerable<int>> SearchAsync(string query)
    {
        return Task.Run(() => Search(query));
    }

    public void RebuildIndex(IEnumerable<Document> documents)
    {
        try
        {
            using var writer = new IndexWriter(_directory, new IndexWriterConfig(_version, _analyzer)
            {
                OpenMode = OpenMode.CREATE // Clears existing index
            });

            foreach (var document in documents)
            {
                var doc = new Lucene.Net.Documents.Document();
                doc.Add(new Lucene.Net.Documents.StringField("Id", document.DocumentId.ToString(), Lucene.Net.Documents.Field.Store.YES));
                
                if (!string.IsNullOrEmpty(document.DocumentName))
                    doc.Add(new Lucene.Net.Documents.TextField("Name", document.DocumentName, Lucene.Net.Documents.Field.Store.YES));
            
                if (!string.IsNullOrEmpty(document.BatchLabel))
                    doc.Add(new Lucene.Net.Documents.TextField("BatchLabel", document.BatchLabel, Lucene.Net.Documents.Field.Store.YES));
            
                if (!string.IsNullOrEmpty(document.CategoryName))
                    doc.Add(new Lucene.Net.Documents.TextField("Category", document.CategoryName, Lucene.Net.Documents.Field.Store.YES));
                
                if (!string.IsNullOrEmpty(document.OcrText))
                    doc.Add(new Lucene.Net.Documents.TextField("Content", document.OcrText, Lucene.Net.Documents.Field.Store.NO));

                writer.AddDocument(doc);
            }
            writer.Commit();
            _logger.LogInformation("Rebuilt Lucene index with {Count} documents", documents.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rebuild index");
        }
    }

    public Task RebuildIndexAsync(IEnumerable<Document> documents)
    {
        return Task.Run(() => RebuildIndex(documents));
    }

    public void Dispose()
    {
        _directory?.Dispose();
        _analyzer?.Dispose();
    }
}
