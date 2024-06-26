// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.18.1

using Azure.AI.OpenAI;
using Azure;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace EchoBot1.Bots
{


    public class EchoBot : ActivityHandler
    {
        private readonly ILogger<EchoBot> _logger;
        private readonly IServiceProvider _serviceProvider;

        public EchoBot(IServiceProvider serviceProvider, ILogger<EchoBot> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }


        // Azure AI Search setup
        static string searchEndpoint = "searchEndpoint"; // Add your Azure AI Search endpoint here
        static string searchKey = "searchKey"; // Add your Azure AI Search admin key here
        static string searchIndexName = "searchIndexName"; // Add your Azure AI Search index name here
        static string searchIndexName2 = "searchIndexName"; // Add your Azure AI Search index name here

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Attachments != null && turnContext.Activity.Attachments.Any())
            {
                _logger.LogInformation("Message has attachments.");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var blobStorageService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
                    var searchService = scope.ServiceProvider.GetRequiredService<SearchService>();

                    foreach (var attachment in turnContext.Activity.Attachments)
                    {
                        _logger.LogInformation($"Processing attachment: {attachment.Name}");
                        using (var fileStream = await GetStreamFromAttachmentAsync(attachment))
                        {
                            await blobStorageService.UploadFileAsync(attachment.Name, fileStream);
                            _logger.LogInformation($"Attachment '{attachment.Name}' uploaded successfully to Azure Blob Storage.");

                            await turnContext.SendActivityAsync(MessageFactory.Text($"File '{attachment.Name}' uploaded successfully to Azure Blob Storage."), cancellationToken);
                        }
                    }
                    await searchService.CreateOrUpdateIndexAsync();
                    await searchService.CreateOrUpdateIndexerAsync();
                }
            }
            else
            {
                await ProcessTextMessageAsync(turnContext, cancellationToken);
            }
        }

        private async Task ProcessTextMessageAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string prompt = turnContext.Activity.Text;
            string resultResponse = await CompletionAPIHandler(prompt);

            if (resultResponse != null)
            {
                // Prepare response with SSML markup for TTS
                var replySpeak = $@"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
                                    <voice name='en-US-JennyMultilingualNeura'>{resultResponse}</voice>
                                </speak>";

                // Send text response with SSML markup
                await turnContext.SendActivityAsync(MessageFactory.Text(resultResponse, ssml: replySpeak), cancellationToken);
            }
            else
            {
                // If completion API fails, send an error message
                await turnContext.SendActivityAsync(MessageFactory.Text("We are having some trouble communicating with our servers. Please try again later"), cancellationToken);
            }
        }


        private async Task<string?> CompletionAPIHandler(string prompt)
        {
            string azureOpenAIEndpoint = "azureOpenAIEndpoint";
            string azureOpenAIKey = "azureOpenAIKey";
            string userMessage = prompt;

            OpenAIClient client = new OpenAIClient(
                new Uri(azureOpenAIEndpoint),
                new AzureKeyCredential(azureOpenAIKey));

            // Load the XML file
            XDocument doc = XDocument.Load("sitemap.xml");
            // Define the XML namespace
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";


            // Query the XML data using LINQ to XML
            // Query the XML data using LINQ to XML with namespace
            var urls = from url in doc.Descendants(ns + "url")
                       select new
                       {
                           Loc = url.Element(ns + "loc").Value
                       };
            string data = "";
            foreach (var url in urls)
            {
                data += $" URLs: {url.Loc}\n";
      
            }


            if (userMessage.Contains("Dalle"))
            {

                Response<ImageGenerations> imageGenerations = await client.GetImageGenerationsAsync(
                    new ImageGenerationOptions()
                    {
                        Prompt = userMessage,
                        Size = ImageSize.Size1024x1024, //only 1024x1024, 1024x1792 or 1792x1024 pixels
                        DeploymentName = "DeploymentName"
                    });

                // Image Generations responses provide URLs you can use to retrieve requested images
                Uri imageUri = imageGenerations.Value.Data[0].Url;

                return $"![image]({imageUri.ToString()})";
            }
            else if (userMessage.Contains("PDF"))
            {

                var site = "";
                if (!String.IsNullOrWhiteSpace(userMessage))
                {
                    int charLocation = userMessage.IndexOf("PDF", StringComparison.Ordinal);

                    if (charLocation > 0)
                    {
                        site = userMessage.Substring(0, charLocation);
                        site = site.Trim();
                    }
                }

                string pdfText = await GetPdfTextFromUrlAsync(site);

                userMessage += pdfText;

                userMessage = userMessage.Replace(site, "");

                ChatCompletionsOptions options = new ChatCompletionsOptions()
                {
                    Messages = { new ChatRequestSystemMessage(@"Your name is bot. You are a helpful assistant. You are given some information from a pdf and thats why PDF is mentioned in the prompt and you must answer"),
                                new ChatRequestUserMessage(userMessage)},
                    Temperature = (float)0.7,
                    MaxTokens = 300,
                    DeploymentName = "DeploymentName",
                };

                while (true)
                {
                    Response<ChatCompletions> response = await client.GetChatCompletionsAsync(options);
                    ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                    ChatChoice responseChoice = response.Value.Choices[0];


                    // Construct final answer with numbered links
                    string finalAnswer = responseChoice.Message.Content;

                    return finalAnswer;
                }
            }
            else if(userMessage.Contains("give me info"))
            {
                userMessage += data;

                ChatCompletionsOptions options = new ChatCompletionsOptions()
                {
                    Messages = { new ChatRequestSystemMessage(@"Your name is bot. You have the sitemap from  [add a sitemap in the project and reference it here] . 
Your task is to generate well-structured responses that correlate the sitemap with specific prompts provided each time and the data collected during the crawl. 
Based on the prompt and the data gathered from the website  ADD absolutely THE THE CLOSEST related SITE TO THE ANSWER BASED ON THE PROMPT AS CITATIONS.
EVERY TIME YOU MENTION VALANTIC YOU MUST ATTACH ONE OF YOUR WEBSITES RELATED TO YOUR SITEMAP"),
                                new ChatRequestUserMessage(userMessage)},
                    Temperature = (float)0.7,
                    MaxTokens = 300,
                    AzureExtensionsOptions = new AzureChatExtensionsOptions()
                    {
                        Extensions =
                         {
                            new AzureCognitiveSearchChatExtensionConfiguration()
                            {
                                SearchEndpoint = new Uri(searchEndpoint),
                                IndexName = searchIndexName,
                                Key  = searchKey,
                                ShouldRestrictResultScope = false,
                                RoleInformation = "ADD THE THE CLOSEST related SITE from the context and sitemap TO THE ANSWER BASED ON THE PROMPT AS CITATIONS, EVERY TIME YOU MENTION VALANTIC YOU MUST ATTACH ONE OF YOUR WEBSITES RELATED TO YOUR SITEMAP.\""

                            }
                        }
                    },
                    DeploymentName = "DeploymentName",
                };

                while (true)
                {
                    Response<ChatCompletions> response = await client.GetChatCompletionsAsync(options);
                    ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                    ChatChoice responseChoice = response.Value.Choices[0];


                    // Add the assistant message with tool calls to the conversation history
                    ChatRequestAssistantMessage toolCallHistoryMessage = new(responseChoice.Message);
                    options.Messages.Add(toolCallHistoryMessage);



                    // Construct final answer with numbered links
                    string finalAnswer = responseChoice.Message.Content;


                    if (responseChoice.Message.Content != null)
                    {
                        string result = Regex.Replace(finalAnswer, @"(\[[\w\-?!\""'$§&]+\])", "");

                        string res = Regex.Replace(result, @"(https?:\/\/)?(www\.)?(site?\.com)(\/(en|de))?(\/([^\/\n\s?,!.]+))?(\/([^\/\n\s?,!.]+))?(\/([^\/\n\s?,!.]+))?", m =>
                        {
                            string originalUrl = m.Groups[0].Value;
                            string group4 = m.Groups[4].Success && !string.IsNullOrEmpty(m.Groups[4].Value) && !m.Groups[4].Value.StartsWith("/") && m.Groups[4].Value != "overview" ? m.Groups[4].Value : m.Groups[3].Value;
                            string group5 = m.Groups[5].Success && !string.IsNullOrEmpty(m.Groups[5].Value) && !m.Groups[5].Value.StartsWith("/") && m.Groups[5].Value != "overview" ? m.Groups[5].Value : group4;
                            string group6 = m.Groups[6].Success && !string.IsNullOrEmpty(m.Groups[6].Value) && !m.Groups[6].Value.StartsWith("/") && m.Groups[6].Value != "overview" ? m.Groups[6].Value : group5;
                            string group7 = m.Groups[7].Success && !string.IsNullOrEmpty(m.Groups[7].Value) && !m.Groups[7].Value.StartsWith("/") && m.Groups[7].Value != "overview" ? m.Groups[7].Value : group6;
                            string group8 = m.Groups[8].Success && !string.IsNullOrEmpty(m.Groups[8].Value) && !m.Groups[8].Value.StartsWith("/") && m.Groups[8].Value != "overview" ? m.Groups[8].Value : group7;
                            string group9 = m.Groups[9].Success && !string.IsNullOrEmpty(m.Groups[9].Value) && !m.Groups[9].Value.StartsWith("/") && m.Groups[9].Value != "overview" ? m.Groups[9].Value : group8;
                            return $"[{group9}]({originalUrl})";
                        });

                        return res;
                    }
                    else return null;

                }
            }
            else
            {
                ChatCompletionsOptions options = new ChatCompletionsOptions()
                {
                    Messages = { new ChatRequestSystemMessage(@"Your name is bot. You are a helpful assistant. You are given some information from a pdf and thats why PDF is mentioned in the prompt and you must answer"),
                                new ChatRequestUserMessage(userMessage)},
                    Temperature = (float)0.7,
                    MaxTokens = 500,
                    AzureExtensionsOptions = new AzureChatExtensionsOptions()
                    {
                        Extensions =
                         {
                            new AzureCognitiveSearchChatExtensionConfiguration()
                            {
                                SearchEndpoint = new Uri(searchEndpoint),
                                IndexName = searchIndexName2,
                                Key  = searchKey,
                                ShouldRestrictResultScope = false
                            }
                        }
                    },
                    DeploymentName = "vba-openai-gpt-4-32k",
                };

                
                var response = await client.GetChatCompletionsAsync(options);
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                ChatChoice responseChoice = response.Value.Choices[0];

                // Construct final answer with numbered links
                string finalAnswer = responseChoice.Message.Content;
                string result = Regex.Replace(finalAnswer, @"(\[[\w\-?!\""'$§&]+\])", "");
                return result;

            }

        }



        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello I am a bot. An AI chatbot made by Ioannis.\n" +
                "If you require to use dalle, you have to add to the input the word 'Dalle', otherwise it will stay as RAG \n" +
                "If you require to use pdf reading via link, you must add this structrute [LINK] PDF [prompt] \n" +
                "Else you can upload files and the assistant will answer depending on them";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }

        public static async Task<string> GetPdfTextFromUrlAsync(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Download the PDF file
                    byte[] pdfBytes = await client.GetByteArrayAsync(url);

                    // Read and extract text from the PDF
                    using (MemoryStream pdfStream = new MemoryStream(pdfBytes))
                    using (PdfReader reader = new PdfReader(pdfStream))
                    {
                        StringWriter text = new StringWriter();

                        for (int i = 1; i <= reader.NumberOfPages; i++)
                        {
                            text.WriteLine(PdfTextExtractor.GetTextFromPage(reader, i));
                        }

                        return text.ToString();
                    }
                }
                catch (Exception ex)
                {
                    // Handle exceptions (e.g., network errors, invalid PDF)
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return string.Empty;
                }
            }
        }

        private async Task<Stream> GetStreamFromAttachmentAsync(Attachment attachment)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(attachment.ContentUrl);
                return await response.Content.ReadAsStreamAsync();
            }
        }

    }
}
