﻿using System;
using System.Linq;
using VsoBackup.Configuration;
using VsoBackup.Logging;
using VsoBackup.Models;
using VsoBackup.Utilities;
using VsoBackup.VisualStudioOnline;

namespace VsoBackup.Services
{
   public class SourceControlBackupService : ISourceControlBackupService
   {
       private readonly IAllConfiguration _allConfiguration;
       private readonly IVsoRestApiService _apiService;
       private readonly IFileSystemService _fileSystemService;
       private readonly ILogger _logger;
       private readonly IGitService _gitService;
     

       public SourceControlBackupService(IAllConfiguration allConfiguration , IVsoRestApiService apiService, IFileSystemService fileSystemService, ILogger logger,IGitService gitService)
       {
           _allConfiguration = allConfiguration;
           _apiService = apiService;
           _fileSystemService = fileSystemService;
           _logger = logger;
           _gitService = gitService;
       }


       public void Backup()
       {
           _logger.WriteLog("Starting run. God speed.");

           if (!StringUtilities.EndsWith(_allConfiguration.FileSystemConfiguration.BasePath, @"\"))
           {    const string ex = "'BasePath' in app.config needs to end with a backslash.";
               throw new Exception(ex);
           }

           var days = _allConfiguration.FileSystemConfiguration.RemoveBackupAfterHowManyDays;
           var pastFolderDate = DateTime.Now.AddDays(-days).ToString("dd-MM-yyyy");;

           if (_fileSystemService.FolderExists(Constants.Today))
           {
               _logger.WriteLog("Folder {0} already exists. Skipping run.", Constants.Today);
               return;
           }

           if (_fileSystemService.BackupTreshholdReached(pastFolderDate))
           {
               _logger.WriteLog("Folder {0} has reached the treshhold date. Folder will be deleted.", pastFolderDate);
               _fileSystemService.DeleteFolder(pastFolderDate);
           }


           var all =_apiService.ExecuteRequest<RootObject>(_allConfiguration.VsoConfiguration.AllRepositoriesUrl).Result.value;
           var groupedByTeamProject = all.GroupBy(m => m.project.name).ToList();

           _logger.WriteLog("Fetched {0} team projects from VSO", groupedByTeamProject.Count);
           _logger.WriteLog("Fetched {0} repositories from VSO", all.Count);
           _fileSystemService.CreateDirectory(Constants.Today);

           foreach (var teamProject in groupedByTeamProject)
           {
               var teamProjectPath = StringUtilities.FormatDateAndTeamProject(Constants.Today, teamProject.Key);
               _fileSystemService.CreateDirectory(teamProjectPath);
               foreach (var repo in teamProject)
               {
                   var path = StringUtilities.FormatDateTeamProjectAndRepository(Constants.Today, teamProject.Key, repo.name);
                   _gitService.Clone(repo, _allConfiguration.FileSystemConfiguration.BasePath + path);
               }
           }
        }
        
     }
    
}
