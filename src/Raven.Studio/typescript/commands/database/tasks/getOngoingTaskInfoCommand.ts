import commandBase = require("commands/commandBase");
import database = require("models/resources/database");
import endpoints = require("endpoints");

class getOngoingTaskInfoCommand<T extends Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskReplication |
                                          Raven.Client.Documents.Subscriptions.SubscriptionStateWithNodeDetails |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskBackup |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskPullReplicationAsSink |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskRavenEtlDetails |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskSqlEtlDetails |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlDetails |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskElasticSearchEtlDetails |
                                          Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskQueueEtlDetails> extends commandBase {

      private db: database;

    private taskType: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType;

    private taskId: number;

    private taskName?: string;

    private reportFailure: boolean = true;

    private constructor(db: database, taskType: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskType,
                        taskId: number, taskName?: string, reportFailure: boolean = true) {
          super();
        this.reportFailure = reportFailure;
        this.taskName = taskName;
        this.taskId = taskId;
        this.taskType = taskType;
        this.db = db;
    }

    execute(): JQueryPromise<T> {
        return this.getTaskInfo()
            .fail((response: JQueryXHR) => {
                if (this.reportFailure) {
                    this.reportError(`Failed to get info for ${this.taskType} task with id: ${this.taskId}.`, response.responseText, response.statusText);
                }
            });
    }

    private getTaskInfo(): JQueryPromise<T> {
        const url = endpoints.databases.ongoingTasks.task;
       
        const args = this.getArgsToUse();

        return this.query<T>(url, args, this.db);
    }

    static forExternalReplication(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskReplication>(db, "Replication", taskId);
    }
    
    static forPullReplicationSink(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskPullReplicationAsSink>(db, "PullReplicationAsSink", taskId);
    }

    static forSubscription(db: database, taskId: number, taskName: string) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Subscriptions.SubscriptionStateWithNodeDetails>(db, "Subscription", taskId, taskName);
    }

    static forBackup(db: database, taskId: number, reportFailure = true) { 
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskBackup>(db, "Backup", taskId, undefined, reportFailure);
    }

    static forRavenEtl(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskRavenEtlDetails>(db, "RavenEtl", taskId);
    }
    
    static forSqlEtl(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskSqlEtlDetails>(db, "SqlEtl", taskId);
    }

    static forOlapEtl(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskOlapEtlDetails>(db, "OlapEtl", taskId);
    }

    static forElasticSearchEtl(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskElasticSearchEtlDetails>(db, "ElasticSearchEtl", taskId);
    }

    private getArgsToUse() {
        if (this.taskName) {
            return {
                key: this.taskId,
                type: this.taskType,
                taskName: this.taskName
            }
        }

        return {
            key: this.taskId,
            type: this.taskType
        }
    }

    static forQueueEtl(db: database, taskId: number) {
        return new getOngoingTaskInfoCommand<Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskQueueEtlDetails>(db, "QueueEtl", taskId);
    }
}

export = getOngoingTaskInfoCommand; 
