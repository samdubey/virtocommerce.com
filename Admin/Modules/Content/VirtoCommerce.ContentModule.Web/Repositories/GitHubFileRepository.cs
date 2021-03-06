﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtoCommerce.ContentModule.Web.Repositories
{
    using System.Reflection.Emit;
    using System.Runtime.InteropServices;
    using System.Web.UI.WebControls;

    using Octokit;
    using Octokit.Models.Request;

    using VirtoCommerce.ContentModule.Web.Model;
    using VirtoCommerce.Framework.Web.Common;

    public class GitHubFileRepository : IFileRepository
    {
        private GitHubClient _client = null;

        private RepositoryInfo _repository = null;

        public GitHubFileRepository(Credentials credentials, RepositoryInfo repository)
        {
            var client = new GitHubClient(new ProductHeaderValue("VirtoCommerce-ContentModule"), new Uri("https://github.com/"))
                         {
                             Credentials
                                 =
                                 credentials
                         };

            _client = client;
            _repository = repository;
        }

        public async Task<ResponseCollection<CollectionItem>> GetCollections()
        {
            var root = await _client.Repository.Contents.GetRoot(_repository.Owner, _repository.Name);

            // convert to Collection Item
            var collectionItems = root.Where(item => item.Type == ContentType.Dir).Select(item => new CollectionItem() { Id = item.Name, Name = item.Path }).ToArray();

            var response = new ResponseCollection<CollectionItem>();
            response.Items.AddRange(collectionItems);
            return response;
        }

        public async Task<ResponseCollection<ContentItem>> GetCollectionItems(string collection, int startIndex, int pageSize)
        {
            var allFiles = await _client.Repository.Contents.GetForPath(_repository.Owner, _repository.Name, collection);

            var collectionItems = allFiles.Select(item => new ContentItem() { Id = item.Name, Status = "Published" });

            var response = new ResponseCollection<ContentItem>();
            response.Items.AddRange(collectionItems);
            return response;
        }

        public async Task<ContentItem> GetContentItem(string collection, string name)
        {
            var allFiles = await _client.Repository.Contents.GetForPath(_repository.Owner, _repository.Name, CreatPath(collection, name));

            var contentItem = allFiles.SingleOrDefault();
            var ret = new ContentItem() { Id = contentItem.Name };

            if (contentItem is FileContent)
            {
                var file = contentItem as FileContent;
                var content64 = Convert.FromBase64String(file.Content);
                ret.Content = Encoding.UTF8.GetString(content64);
            }

            return ret;
        }

        public async Task<ContentItem> SaveContentItem(string collection, string name, ContentItem item)
        {
            // Determine if file exists
            var existingItem = await GetItem(collection, name);

            var path = CreatPath(collection, item.Id);
            var contentUTF8Bytes = Encoding.UTF8.GetBytes(item.Content);
            var sha = String.Empty;

            if (existingItem == null) // create new
            {
                var response = await _client.Repository.Contents.CreateFile(
                    _repository.Owner,
                    _repository.Name,
                    path,
                    new CreateFileRequest() { Message = "Updating file from admin", Content = Convert.ToBase64String(contentUTF8Bytes) });                               
            }
            else // update existing
            {
                var response = await _client.Repository.Contents.UpdateFile(
                    _repository.Owner,
                    _repository.Name,
                    path,
                    new UpdateFileRequest() { Message = "Updating file from admin", Content = Convert.ToBase64String(contentUTF8Bytes), Sha = existingItem.Sha});               
            }
            
         
            /*
            var repository = await _client.Repository.Get(_repository.Owner, _repository.Name);

            var files = new Dictionary<string, string> { {String.Format("{0}/{1}", collection, item.Id), item.Content} };

            var reference = await _client.SaveFiles(repository, "website checkin", files);

            return item;
             * */

            return item;
        }

        public async Task DeleteContentItem(string collection, string name)
        {
            var existingItem = await GetItem(collection, name);
            var path = CreatPath(collection, name);
            if (existingItem != null)
            {
                await
                        _client.Repository.Contents.DeleteFile(
                            _repository.Owner,
                            _repository.Name,
                            path,
                            new DeleteFileRequest() { Message = "Updating file from admin", Sha = existingItem.Sha });
            }
        }

        private async Task<DirectoryContent> GetItem(string collection, string name)
        {
            try
            {
                var existingItems = await _client.Repository.Contents.GetForPath(_repository.Owner, _repository.Name, CreatPath(collection, name));
                if (existingItems.Count == 0) return null;
                return existingItems.SingleOrDefault();
            }
            catch (NotFoundException)
            {
                return null;
            }
        }

        private string CreatPath(string collection, string fileName)
        {
            var path = String.IsNullOrEmpty(collection) ? fileName : String.Format("{0}/{1}", collection, fileName);
            return path;
        }
    }

    public class RepositoryInfo
    {
        public RepositoryInfo(string owner, string name)
        {
            this.Owner = owner;
            this.Name = name;
        }
        public string Owner { get; private set; }
        public string Name { get; private set; }
    }
}
