using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods.Token;
using Google.Cloud.Storage.V1;
using Google.Cloud.PubSub.V1;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace gcs_watch_net
{
    public struct SecretMetadata
    {
                public string Name { get; set; }
                public int Version { get; set; }
                public bool IsDeleted { get; set; }
                public override string ToString() =>
                    $"Name - {Name} | Version - {Version} | IsDeleted - {IsDeleted}";
    }

    public class Startup
    {
#region constants
        const string ProjectId = "api-7805202409062569548-609752";
        const string BucketName = "testbuck1111111";
        const string SubsciptionId =  "vaultsb";
#endregion
        public async Task<IEnumerable<SecretMetadata>> GetSecretMetadataFromBucket()
        {
            var client = await StorageClient.CreateAsync();
            MemoryStream ms = new MemoryStream();
            await client.DownloadObjectAsync(BucketName,"data.json",ms);
            ms.Position = 0;
            var rawJsonContent = Encoding.Default.GetString(ms.ToArray());
            var data = JsonConvert.DeserializeObject<IEnumerable<SecretMetadata>>(rawJsonContent);
            return data;
        }

        public async Task InitPubSub()
        {
            Console.WriteLine("Pubsub - started");
            SubscriberServiceApiClient subscriberService = await SubscriberServiceApiClient.CreateAsync();
            SubscriptionName subscriptionName = new SubscriptionName(ProjectId, SubsciptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);
            await subscriber.StartAsync(async (msg,token) =>
            {
                Console.WriteLine("fired pubsubchange");
                var attributes = msg.Attributes.Values;
                var changedMetadatas =  await GetDiffOfSecrets();
                if(changedMetadatas?.Count() > 0)
                {
                    foreach(var attrib in attributes)
                    {
                        if(attrib.Contains("metadata") )
                        {
                            var deletedMetadatas = changedMetadatas.Where(
                                    m => m.IsDeleted = true
                                    );
                            if(deletedMetadatas?.Count() > 0)
                            {
                                deletedMetadatas.ToList().ForEach(
                                        dm => Console.WriteLine(dm)
                                        );
                            }
                            return SubscriberClient.Reply.Ack;
                        }
                    }
                    changedMetadatas.ToList().ForEach(m => Console.WriteLine(
                                    m
                                ));
                }
                return SubscriberClient.Reply.Ack;
            });
        }
        public async Task SaveSecretMetadataToBucket(string jsonData)
        {
            try
            {
                var client = await StorageClient.CreateAsync();
                var content = Encoding.UTF8.GetBytes(jsonData);
                await client.UploadObjectAsync(BucketName,
                                                    "data.json",
                                                    "text/plain",
                                                    new MemoryStream(content));
            }
            catch(Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }

        public async Task<IEnumerable<SecretMetadata>> GetDiffOfSecrets()
        {
            List<SecretMetadata> metadatas = new List<SecretMetadata>();
            IEnumerable<SecretMetadata> changedMetadatas = new List<SecretMetadata>();
            try
            {
                var vaultSettings = new VaultClientSettings(Constants.HostWithConsul,
                                     new TokenAuthMethodInfo(Constants.TokenWithConsul));
                var client = new VaultClient(vaultSettings);
                var root = "kv";
                var projectsRoot = Constants.ProjectsRootPath;
                var keys = client.V1.Secrets.KeyValue.V2.ReadSecretPathsAsync(
                        path: $"{projectsRoot}",mountPoint: root).Result;
                foreach(var key in keys.Data.Keys)
                {
                    var keyValue = client.V1.Secrets.KeyValue.V2.ReadSecretMetadataAsync(
                       path: $"{projectsRoot}/{key}", mountPoint:root).Result;
                    var versions = keyValue.Data.Versions;
                    foreach(var version in versions)
                    {
                       var versionNum = int.Parse(version.Key);
                       var isDeleted = !string.IsNullOrWhiteSpace(
                               version.Value.DeletionTime);
                       metadatas.Add(new SecretMetadata
                       {
                           Name = key,
                           Version = versionNum,
                           IsDeleted = isDeleted
                       });
                    }
                }

                var oldMetadatas = await GetSecretMetadataFromBucket();
                oldMetadatas.ToList().ForEach(el=>Console.WriteLine(el));
                Console.WriteLine("new metadatas");
                metadatas.ToList().ForEach(el=>Console.WriteLine(el));

                changedMetadatas = metadatas.Except(oldMetadatas);
                var jsonData = JsonConvert.SerializeObject(metadatas);
                await SaveSecretMetadataToBucket(jsonData);
            }
            catch(Exception exc)
            {
                Console.WriteLine(exc);
            }
            finally
            {

            }
            return changedMetadatas;
        }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(
                    CompatibilityVersion.Version_2_1);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }
            app.UseMvc();
            Task.Run(async () => {
                await InitPubSub();
            });

        }
    }
}
