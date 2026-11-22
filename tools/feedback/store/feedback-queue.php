<?php
declare(strict_types=1);
require_once __DIR__.'/database.php';
require_once __DIR__.'/support.php';
require_once __DIR__.'/private-logs.php';
require_once __DIR__.'/github.php';
require_once __DIR__.'/admin-totp.php';
const FEEDBACK_REPOSITORY = 'sandmaennchen5/fork_NxFileViewer';
function feedbackQuery(PDO $db,string $sql,array $parameters=[]): PDOStatement {
    $query=$db->prepare($sql);$query->execute($parameters);return $query;
}
function feedbackSchema(PDO $db): void {
    databaseSqliteSchema($db,"CREATE TABLE IF NOT EXISTS feedback_queue (
        ticket TEXT PRIMARY KEY, request_key TEXT NOT NULL UNIQUE, mode TEXT NOT NULL,
        title TEXT NOT NULL, body TEXT NOT NULL, internal_id TEXT NULL, support_id TEXT NOT NULL,
        display_name TEXT NOT NULL, status TEXT NOT NULL, number INTEGER NULL, author TEXT NOT NULL DEFAULT '',
        credential TEXT NULL, credential_expires INTEGER NOT NULL DEFAULT 0, attempts INTEGER NOT NULL DEFAULT 0,
        next_attempt INTEGER NOT NULL, lease_until INTEGER NOT NULL DEFAULT 0, last_error TEXT NOT NULL DEFAULT '',
        created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL);
        CREATE INDEX IF NOT EXISTS feedback_due ON feedback_queue(status,next_attempt);
        CREATE INDEX IF NOT EXISTS feedback_created ON feedback_queue(created_at)");
    $db->exec(databaseSql($db,
        "CREATE TABLE IF NOT EXISTS feedback_system_details (ticket TEXT PRIMARY KEY, details TEXT NOT NULL)",
        "CREATE TABLE IF NOT EXISTS feedback_system_details (".databaseTables()['feedback_system_details'].") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin"));
}
function feedbackFind(PDO $db,string $ticket): ?array {
    return feedbackQuery($db,'SELECT * FROM feedback_queue WHERE ticket=?',[$ticket])->fetch(PDO::FETCH_ASSOC)?:null;
}
function feedbackSystemDetails(PDO $db,string $ticket): ?string {
    $details=feedbackQuery($db,'SELECT details FROM feedback_system_details WHERE ticket=?',[$ticket])->fetchColumn();
    return $details===false?null:$details;
}
function feedbackMarker(string $ticket): string {return '<!-- nx-feedback:'.$ticket.' -->';}
function feedbackCredential(array $config,string $token): string {
    $iv=random_bytes(12);$tag='';$cipher=openssl_encrypt($token,'aes-256-gcm',adminCryptoKey($config,'feedback-queue'),OPENSSL_RAW_DATA,$iv,$tag);
    if($cipher===false)throw new RuntimeException('Credential encryption failed');return base64_encode($iv.$tag.$cipher);
}
function feedbackUnlock(array $config,string $encrypted): string {
    $raw=base64_decode($encrypted,true);if($raw===false||strlen($raw)<29)throw new RuntimeException('Invalid credential');
    $token=openssl_decrypt(substr($raw,28),'aes-256-gcm',adminCryptoKey($config,'feedback-queue'),OPENSSL_RAW_DATA,substr($raw,0,12),substr($raw,12,16));
    if($token===false)throw new RuntimeException('Credential unavailable');return $token;
}
function feedbackStore(PDO $db,string $ticket,string $requestKey,string $mode,string $title,string $body,?array $profile,string $name,?array $log,?string $systemDetails=null): array {
    $db->beginTransaction();
    try{
        feedbackQuery($db,'INSERT INTO feedback_queue (ticket,request_key,mode,title,body,internal_id,support_id,display_name,status,next_attempt,created_at,updated_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)',
            [$ticket,$requestKey,$mode,$title,$body,$profile['internalId']??null,$profile['supportId']??'',$name,$mode==='github'?'awaiting_auth':'queued',time(),time(),time()]);
        if($systemDetails!==null)
            feedbackQuery($db,'INSERT INTO feedback_system_details (ticket,details) VALUES (?,?)',[$ticket,$systemDetails]);
        privateLogSave($db,$ticket,$log);$db->commit();return feedbackFind($db,$ticket);
    }catch(Throwable $error){if($db->inTransaction())$db->rollBack();throw $error;}
}
// Recover anonymous display names previously replaced with the delivery bot login.
function feedbackRepairDisplayNames(PDO $db): void {
    feedbackQuery($db,"UPDATE support_issues SET display_name=(SELECT q.display_name FROM feedback_queue q WHERE q.number=support_issues.issue_number AND q.mode='anonymous' ORDER BY q.created_at DESC,q.ticket DESC LIMIT 1)
        WHERE EXISTS (SELECT 1 FROM feedback_queue q WHERE q.number=support_issues.issue_number AND q.mode='anonymous' AND q.author<>'' AND q.author=support_issues.display_name)");
    feedbackQuery($db,"UPDATE support_profiles SET display_name=(SELECT q.display_name FROM feedback_queue q WHERE q.internal_id=support_profiles.internal_id AND q.status='created' ORDER BY q.created_at DESC,q.ticket DESC LIMIT 1)
        WHERE EXISTS (SELECT 1 FROM feedback_queue q WHERE q.internal_id=support_profiles.internal_id AND q.mode='anonymous' AND q.status='created' AND q.author<>'' AND q.author=support_profiles.display_name
        AND q.ticket=(SELECT latest.ticket FROM feedback_queue latest WHERE latest.internal_id=support_profiles.internal_id AND latest.status='created' ORDER BY latest.created_at DESC,latest.ticket DESC LIMIT 1))");
}

function feedbackDeleteArchived(PDO $db,string $ticket): bool {
    $db->beginTransaction();
    try {
        // Deleting the queue row first prevents a new delivery claim. In-flight sends are protected.
        $deleted=feedbackQuery($db,"DELETE FROM feedback_queue WHERE ticket=? AND status NOT IN ('preparing','sending')",[$ticket]);
        if($deleted->rowCount()!==1){$db->rollBack();return false;}
        feedbackQuery($db,'DELETE FROM private_issue_logs WHERE ticket=?',[$ticket]);
        feedbackQuery($db,'DELETE FROM feedback_system_details WHERE ticket=?',[$ticket]);
        feedbackQuery($db,'DELETE FROM reports WHERE ticket=?',[$ticket]);
        $db->commit();return true;
    }catch(Throwable $error){if($db->inTransaction())$db->rollBack();throw $error;}
}

function feedbackComplete(PDO $db,array $report,int $number,string $author=''): void {
    if($number<1)throw new RuntimeException('Invalid issue number');
    $db->beginTransaction();
    try{
        if($report['internal_id']!==null) supportRecordIssue($db,['internalId'=>$report['internal_id'],'supportId'=>$report['support_id']],$number,$report['mode']==='anonymous'?$report['display_name']:($author!==''?$author:$report['display_name']));
        privateLogLink($db,$report['ticket'],$number);
        feedbackQuery($db,"UPDATE feedback_queue SET status='created',number=?,author=?,credential=NULL,credential_expires=0,lease_until=0,last_error='',updated_at=? WHERE ticket=?",[$number,$author,time(),$report['ticket']]);
        $db->commit();
    }catch(Throwable $error){if($db->inTransaction())$db->rollBack();throw $error;}
}
function feedbackRetry(PDO $db,array $report,string $reason,int $now): void {
    $attempts=(int)$report['attempts'];$delay=min(3600,60*(2**min(6,$attempts-1)));
    feedbackQuery($db,'UPDATE feedback_queue SET status=?,last_error=?,next_attempt=?,lease_until=0,updated_at=? WHERE ticket=? AND status IN (\'preparing\',\'sending\')',
        [$attempts>=12?'blocked':'queued',$reason,$now+$delay,$now,$report['ticket']]);
}
/** Claim before contacting GitHub. Never automatically repeat a possibly accepted POST. */
function feedbackDeliver(PDO $db,array $config,string $ticket,?callable $credentials=null,?callable $create=null,?int $now=null): array {
    $now??=time();
    feedbackQuery($db,"UPDATE feedback_queue SET status='review',last_error='delivery_unconfirmed',lease_until=0,updated_at=? WHERE ticket=? AND status='sending' AND lease_until<=?",[$now,$ticket,$now]);
    feedbackQuery($db,"UPDATE feedback_queue SET status='queued',lease_until=0 WHERE ticket=? AND status='preparing' AND lease_until<=?",[$ticket,$now]);
    $claim=feedbackQuery($db,"UPDATE feedback_queue SET status='preparing',lease_until=?,attempts=attempts+1,updated_at=? WHERE ticket=? AND status='queued' AND next_attempt<=?",[$now+120,$now,$ticket,$now]);
    $report=feedbackFind($db,$ticket);if(!$report)throw new RuntimeException('Report missing');if($claim->rowCount()!==1)return $report;
    try{
        if($credentials!==null)$token=$credentials($report);
        elseif($report['mode']==='anonymous')$token=installationToken($config);
        elseif($report['credential']!==null&&(int)$report['credential_expires']>$now+30)$token=feedbackUnlock($config,$report['credential']);
        else{
            feedbackQuery($db,"UPDATE feedback_queue SET status='needs_auth',credential=NULL,last_error='authorization_required',lease_until=0,updated_at=? WHERE ticket=? AND status='preparing'",[$now,$ticket]);return feedbackFind($db,$ticket);
        }
    }catch(Throwable $error){feedbackRetry($db,$report,'github_credentials_unavailable',$now);return feedbackFind($db,$ticket);}
    $send=feedbackQuery($db,"UPDATE feedback_queue SET status='sending' WHERE ticket=? AND status='preparing' AND lease_until=?",[$ticket,$now+120]);
    if($send->rowCount()!==1)return feedbackFind($db,$ticket);
    try{
        $payload=['title'=>$report['title'],'body'=>$report['body']."\n\n".feedbackMarker($ticket)];
        $issue=$create!==null?$create($payload,$token):github('https://api.github.com/repos/'.FEEDBACK_REPOSITORY.'/issues',$payload,$token);
        if(!is_int($issue['number']??null)||$issue['number']<1)throw new RuntimeException('Invalid issue response');
        feedbackComplete($db,$report,$issue['number'],(string)($issue['user']['login']??''));
    }catch(Throwable $error){
        // An explicit 4xx rejection did not create an issue. Timeouts / 5xx remain uncertain.
        if($error instanceof GitHubRequestError && in_array($error->httpStatus,[401,403,429],true)){
            if($report['mode']==='github'&&$error->httpStatus===401)feedbackQuery($db,"UPDATE feedback_queue SET status='needs_auth',credential=NULL,last_error='authorization_required',lease_until=0,updated_at=? WHERE ticket=?",[$now,$ticket]);
            else feedbackRetry($db,$report,'github_rejected_temporarily',$now);
        }elseif($error instanceof GitHubRequestError&&$error->httpStatus>=400&&$error->httpStatus<500){
            feedbackQuery($db,"UPDATE feedback_queue SET status='blocked',credential=NULL,last_error='github_rejected',lease_until=0,updated_at=? WHERE ticket=?",[$now,$ticket]);
        }else feedbackQuery($db,"UPDATE feedback_queue SET status='review',last_error='delivery_unconfirmed',lease_until=0,updated_at=? WHERE ticket=?",[$now,$ticket]);
    }
    return feedbackFind($db,$ticket);
}
function feedbackRun(PDO $db,array $config,int $limit=10): array {
    feedbackSchema($db);feedbackQuery($db,'UPDATE feedback_queue SET credential=NULL WHERE credential IS NOT NULL AND credential_expires<=?',[time()]);$rows=feedbackQuery($db,"SELECT ticket FROM feedback_queue WHERE (status='queued' AND next_attempt<=?) OR (status IN ('preparing','sending') AND lease_until<=?) ORDER BY created_at LIMIT ".max(1,min(20,$limit)),[time(),time()])->fetchAll(PDO::FETCH_COLUMN);
    $states=[];$started=time();foreach($rows as $ticket){if(time()-$started>45)break;$report=feedbackDeliver($db,$config,$ticket);$states[$report['status']]=($states[$report['status']]??0)+1;}return $states;
}
function feedbackPublic(array $report): array {
    return ['status'=>in_array($report['status'],['awaiting_auth','preparing','sending'],true)?'pending':$report['status'],
        'receipt'=>substr($report['ticket'],0,12),'number'=>$report['number']===null?null:(int)$report['number'],'login'=>$report['author']?:null,'supportId'=>$report['support_id']?:null];
}

function feedbackBody(string $mode,string $body,string $name,string $supportId): string {
    if($mode==='anonymous'){
        $body.="\n\n_Gemeldet über NxFileViewer ohne GitHub-Konto._";
        if($name!=='')$body.="\n\nAnzeigename (frei gewählt):\n\n    ".$name;
    }
    if($supportId!=='')$body.="\n\nSupport-ID: ".$supportId;
    return $body;
}
