<?php
declare(strict_types=1);

const REPOSITORY = 'sandmaennchen5/fork_NxFileViewer';

function reply(array $data, int $status = 200): never
{
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($data, JSON_THROW_ON_ERROR);
    exit;
}

function browserReply(string $message, int $status = 200): never
{
    http_response_code($status);
    header('Content-Type: text/plain; charset=utf-8');
    echo $message;
    exit;
}

function randomToken(): string
{
    return bin2hex(random_bytes(32));
}

function base64Url(string $bytes): string
{
    return rtrim(strtr(base64_encode($bytes), '+/', '-_'), '=');
}

function execute(PDO $db, string $sql, array $parameters = []): PDOStatement
{
    $statement = $db->prepare($sql);
    $statement->execute($parameters);
    return $statement;
}

require_once dirname(__DIR__) . '/store/github.php';

function authorizationReply(PDO $db,array $config,array $report,string $ticket,bool $privateStored): never {
    $state=randomToken();$verifier=randomToken();$hashedState=hash('sha256',$state);
    execute($db,databaseSql($db,"INSERT INTO reports (ticket,state,verifier,status,expires) VALUES (?,?,?,'pending',?) ON CONFLICT(ticket) DO UPDATE SET state=excluded.state,verifier=excluded.verifier,status='pending',expires=excluded.expires",
        "INSERT INTO reports (ticket,state,verifier,status,expires) VALUES (?,?,?,'pending',?) ON DUPLICATE KEY UPDATE state=VALUES(state),verifier=VALUES(verifier),status='pending',expires=VALUES(expires)"),[$report['ticket'],$hashedState,$verifier,time()+650]);
    $authorization='https://github.com/login/oauth/authorize?'.http_build_query(['client_id'=>$config['client_id'],'redirect_uri'=>$config['callback_url'],'state'=>$state,'code_challenge'=>base64Url(hash('sha256',$verifier,true)),'code_challenge_method'=>'S256'],encoding_type:PHP_QUERY_RFC3986);
    reply(feedbackPublic($report)+['authorization'=>$authorization,'ticket'=>$ticket,'privateLogStored'=>$privateStored,'systemDetailsStored'=>feedbackSystemDetails($db,$report['ticket'])!==null],202);
}
function handleCallback(PDO $db,array $config): never {
    $state=$_GET['state']??'';
    if(!is_string($state)||!preg_match('/\A[a-f0-9]{64}\z/',$state))browserReply('Invalid authorization request.',400);
    $hash=hash('sha256',$state);
    // A sign-in request is separate from any report or log upload.
    $session=execute($db,'SELECT * FROM github_signins WHERE state=?',[$hash])->fetch(PDO::FETCH_ASSOC);
    if($session){
        $claim=execute($db,"UPDATE github_signins SET status='processing' WHERE state=? AND status='pending' AND expires>?",[$hash,time()]);
        if($claim->rowCount()!==1)browserReply('This sign-in request expired or was already used.',400);
        try{
            $code=$_GET['code']??'';
            if(!is_string($code)||$code===''||strlen($code)>256)throw new RuntimeException('Authorization canceled');
            $user=github('https://github.com/login/oauth/access_token',['client_id'=>$config['client_id'],'client_secret'=>$config['client_secret'],'code'=>$code,'code_verifier'=>$session['verifier'],'redirect_uri'=>$config['callback_url'],'repository_id'=>$config['repository_id']]);
            if(!is_string($user['access_token']??null)||$user['access_token']==='')throw new RuntimeException('Authorization failed');
            $profile=github('https://api.github.com/user',[],$user['access_token'],'GET');
            $login=$profile['login']??null;
            if(!is_string($login)||!preg_match('/\A[a-zA-Z0-9-]{1,39}\z/',$login))throw new RuntimeException('GitHub account unavailable');
            $expires=time()+min(600,max(0,(int)($user['expires_in']??3600)-30));
            execute($db,"UPDATE github_signins SET credential=?,expires=?,login=?,status='authorized',verifier='' WHERE ticket=?",[feedbackCredential($config,$user['access_token']),$expires,$login,$session['ticket']]);
            browserReply('GitHub sign-in successful. Return to NxFileViewer and click Report with GitHub account to send your report. Nothing has been sent yet.');
        }catch(Throwable $error){
            execute($db,"UPDATE github_signins SET status='failed',verifier='',credential=NULL WHERE ticket=?",[$session['ticket']]);
            browserReply('GitHub sign-in could not be completed. Return to NxFileViewer and try again. Nothing has been sent.',400);
        }
    }
    $claim=execute($db,"UPDATE reports SET status='processing' WHERE state=? AND status='pending' AND expires>?",[$hash,time()]);
    if($claim->rowCount()!==1)browserReply('This authorization request has expired or was already used. Your saved report remains available to the maintainer.',400);
    $oauth=execute($db,'SELECT ticket,verifier FROM reports WHERE state=?',[$hash])->fetch(PDO::FETCH_ASSOC);
    $report=feedbackFind($db,$oauth['ticket']);
    if(!$report||!in_array($report['status'],['awaiting_auth','needs_auth'],true))browserReply('This report is already being processed.',409);
    try{
        $code=$_GET['code']??'';if(!is_string($code)||$code===''||strlen($code)>256)throw new RuntimeException('Authorization canceled');
        $user=github('https://github.com/login/oauth/access_token',['client_id'=>$config['client_id'],'client_secret'=>$config['client_secret'],'code'=>$code,'code_verifier'=>$oauth['verifier'],'redirect_uri'=>$config['callback_url'],'repository_id'=>$config['repository_id']]);
        if(!is_string($user['access_token']??null)||$user['access_token']==='')throw new RuntimeException('Authorization failed');
        $encrypted=feedbackCredential($config,$user['access_token']);
        $expires=time()+min(28800,max(60,(int)($user['expires_in']??3600)));
        execute($db,"UPDATE feedback_queue SET credential=?,credential_expires=?,status='queued',next_attempt=?,last_error='',updated_at=? WHERE ticket=? AND status IN ('awaiting_auth','needs_auth')",[$encrypted,$expires,time(),time(),$report['ticket']]);
        execute($db,"UPDATE reports SET status='authorized',verifier='' WHERE ticket=?",[$report['ticket']]);
        $report=feedbackDeliver($db,$config,$report['ticket']);
        browserReply($report['status']==='created'?'GitHub issue #'.$report['number'].' created. Your report is also saved for the maintainer.':'Your report is saved for the maintainer. GitHub delivery is pending or requires review. You can return to NxFileViewer.');
    }catch(Throwable $error){
        execute($db,"UPDATE reports SET status='failed',verifier='' WHERE ticket=?",[$report['ticket']]);
        execute($db,"UPDATE feedback_queue SET status='needs_auth',last_error='authorization_unavailable',updated_at=? WHERE ticket=? AND status IN ('awaiting_auth','needs_auth')",[time(),$report['ticket']]);
        browserReply('Your report is saved for the maintainer. GitHub authorization could not be completed. Retry authorization from NxFileViewer.',200);
    }
}

header('Cache-Control: no-store');
header('Referrer-Policy: no-referrer');
header('X-Content-Type-Options: nosniff');
ini_set('display_errors', '0');

try {
    $config = require dirname(__DIR__) . '/store/config.php';
    require_once dirname(__DIR__) . '/store/database.php';
    require_once dirname(__DIR__) . '/store/support.php';
    require_once dirname(__DIR__) . '/store/private-logs.php';
    require_once dirname(__DIR__) . '/store/feedback-queue.php';
    $db = databaseOpen($config);
    supportSchema($db);
    privateLogSchema($db);
    feedbackSchema($db);
    databaseSqliteSchema($db,'CREATE TABLE IF NOT EXISTS reports (ticket TEXT PRIMARY KEY,state TEXT UNIQUE,verifier TEXT,status TEXT,expires INTEGER)');
    databaseSqliteSchema($db, 'CREATE INDEX IF NOT EXISTS report_expiry ON reports(expires)');
    databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS rate_limit (
        bucket TEXT PRIMARY KEY, count INTEGER, expires INTEGER
    )');
    execute($db, 'DELETE FROM reports WHERE expires <= ?', [time()]);
    $db->exec(databaseSql($db,
        "CREATE TABLE IF NOT EXISTS github_signins (ticket TEXT PRIMARY KEY,state TEXT UNIQUE,verifier TEXT,status TEXT,credential TEXT NULL,expires INTEGER)",
        "CREATE TABLE IF NOT EXISTS github_signins (ticket CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,state CHAR(64) CHARACTER SET ascii COLLATE ascii_bin UNIQUE,verifier VARCHAR(128),status VARCHAR(16),credential TEXT NULL,expires BIGINT) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin"));
    // Upgrade existing sign-in sessions without replacing their table.
    $columns=$db->query(databaseIsMariaDb($db)?'SHOW COLUMNS FROM github_signins':'PRAGMA table_info(github_signins)')->fetchAll(PDO::FETCH_ASSOC);
    if(!in_array('login',array_column($columns,databaseIsMariaDb($db)?'Field':'name'),true)){
        try{$db->exec("ALTER TABLE github_signins ADD COLUMN login VARCHAR(39) NOT NULL DEFAULT ''");}
        catch(PDOException $error){$db->query('SELECT login FROM github_signins LIMIT 0');}
    }
    execute($db,'DELETE FROM github_signins WHERE expires<=?',[time()]);


    $method = $_SERVER['REQUEST_METHOD'] ?? '';
    if ($method === 'GET') {
        handleCallback($db, $config);
    }
    if ($method !== 'POST') {
        header('Allow: GET, POST');
        reply(['error' => 'method'], 405);
    }
    $raw = file_get_contents('php://input', false, null, 0, 8388609);
    if ($raw === false || strlen($raw) > 8388608) {
        reply(['error' => 'size'], 413);
    }
    $input = json_decode($raw, true);
    $mode = $input['mode'] ?? '';
    if($mode==='signin_status'){
        $ticket=$input['ticket']??'';
        if(!is_string($ticket)||!preg_match('/\A[a-f0-9]{64}\z/',$ticket))reply(['error'=>'ticket'],400);
        $session=execute($db,'SELECT status,login FROM github_signins WHERE ticket=? AND expires>?',[hash('sha256',$ticket),time()])->fetch(PDO::FETCH_ASSOC);
        reply(['status'=>$session['status']??'expired','login'=>($session['status']??'')==='authorized'?$session['login']:null]);
    }
    if($mode==='signin'){
        // Bound unauthenticated sign-in requests independently of report quotas.
        $bucket='signin:'.hash_hmac('sha256',intdiv(time(),3600).':'.($_SERVER['REMOTE_ADDR']??''),$config['client_secret']);
        execute($db,'DELETE FROM rate_limit WHERE expires<=?',[time()]);
        execute($db,databaseSql($db,'INSERT INTO rate_limit (bucket,count,expires) VALUES (?,1,?) ON CONFLICT(bucket) DO UPDATE SET count=count+1','INSERT INTO rate_limit (bucket,count,expires) VALUES (?,1,?) ON DUPLICATE KEY UPDATE count=count+1'),[$bucket,time()+3600]);
        if((int)execute($db,'SELECT count FROM rate_limit WHERE bucket=?',[$bucket])->fetchColumn()>20)reply(['error'=>'rate_limit'],429);
        $ticket=randomToken();$state=randomToken();$verifier=randomToken();
        execute($db,"INSERT INTO github_signins (ticket,state,verifier,status,expires) VALUES (?,?,?,'pending',?)",[hash('sha256',$ticket),hash('sha256',$state),$verifier,time()+650]);
        $authorization='https://github.com/login/oauth/authorize?'.http_build_query(['client_id'=>$config['client_id'],'redirect_uri'=>$config['callback_url'],'state'=>$state,'code_challenge'=>base64Url(hash('sha256',$verifier,true)),'code_challenge_method'=>'S256'],encoding_type:PHP_QUERY_RFC3986);
        reply(['authorization'=>$authorization,'ticket'=>$ticket],202);
    }
    if ($mode === 'status') {
        $ticket = $input['ticket'] ?? '';
        if (!is_string($ticket) || !preg_match('/\A[a-f0-9]{64}\z/', $ticket)) {
            reply(['error' => 'ticket'], 400);
        }
        $report=feedbackFind($db,hash('sha256',$ticket));
        if($report){
            if($report['status']==='awaiting_auth'&&!execute($db,"SELECT 1 FROM reports WHERE ticket=? AND status='pending' AND expires>?",[$report['ticket'],time()])->fetchColumn()){
                execute($db,"UPDATE feedback_queue SET status='needs_auth',last_error='authorization_required',updated_at=? WHERE ticket=? AND status='awaiting_auth'",[time(),$report['ticket']]);$report=feedbackFind($db,$report['ticket']);
            }
            reply(feedbackPublic($report));
        }
        reply(['error'=>'expired'],404);

    }

    $participant = $input['participant'] ?? null;
    if ($participant !== null && (!is_string($participant) || !preg_match('/\A[a-f0-9]{64}\z/', $participant))) {
        reply(['error' => 'invalid_participant'], 400);
    }
    if ($mode === 'identity') {
        if ($participant === null) reply(['error' => 'invalid_participant'], 400);
        $secret = $config['support_secret'] ?? $config['poll_secret'] ?? '';
        if (strlen($secret) < 32) throw new RuntimeException('Configure support_secret');
        databaseSqliteSchema($db, 'CREATE TABLE IF NOT EXISTS support_rate (bucket TEXT PRIMARY KEY, count INTEGER, expires INTEGER)');
        execute($db, 'DELETE FROM support_rate WHERE expires < ?', [time()]);
        $bucket = hash_hmac('sha256', intdiv(time(), 3600) . ':' . ($_SERVER['REMOTE_ADDR'] ?? ''), $secret);
        execute($db, databaseSql($db, 'INSERT INTO support_rate (bucket, count, expires) VALUES (?, 1, ?)
            ON CONFLICT(bucket) DO UPDATE SET count = count + 1', 'INSERT INTO support_rate (bucket, count, expires) VALUES (?, 1, ?)
            ON DUPLICATE KEY UPDATE count = count + 1'), [$bucket, time() + 3600]);
        if (execute($db, 'SELECT count FROM support_rate WHERE bucket = ?', [$bucket])->fetchColumn() > 120)
            reply(['error' => 'rate_limit'], 429);
        $profile = supportProfile($db, $config, $participant);
        reply(['supportId' => $profile['supportId']]);
    }

    $signin=null;
    if($mode==='github'&&array_key_exists('signinTicket',$input)){
        $signinTicket=$input['signinTicket'];
        if(!is_string($signinTicket)||!preg_match('/\A[a-f0-9]{64}\z/',$signinTicket))reply(['error'=>'signin_required'],401);
        $signin=execute($db,"SELECT credential,expires FROM github_signins WHERE ticket=? AND status='authorized' AND expires>?",[hash('sha256',$signinTicket),time()+30])->fetch(PDO::FETCH_ASSOC);
        if(!$signin||$signin['credential']===null)reply(['error'=>'signin_required'],401);
    }
    $title = $input['title'] ?? null;
    $body = $input['body'] ?? null;
    $displayName = $input['displayName'] ?? '';
    if (!is_string($displayName)) reply(['error' => 'invalid_display_name'], 400);
    $displayName = trim($displayName);
    if ($mode === 'anonymous' && $displayName !== '' &&
        !preg_match('/\A[\p{L}\p{N} ._-]{1,40}\z/u', $displayName)) {
        reply(['error' => 'invalid_display_name'], 400);
    }
    if (!in_array($mode, ['github', 'anonymous'], true) ||
        !is_string($title) || !is_string($body) || trim($title) === '' ||
        trim($body) === '' || strlen($title) > 640 || strlen($body) > 24000 ||
        str_contains($title, "\n") || str_contains($title, "\r")) {
        reply(['error' => 'invalid_report'], 400);
    }

    try {
        $privateLog = privateLogDecode($input['privateLog'] ?? null);
        if ($privateLog !== null && $participant === null) reply(['error' => 'invalid_participant'], 400);
    } catch (InvalidArgumentException $error) {
        reply(['error' => 'invalid_private_log'], 400);
    }

    $systemDetails=$input['systemDetails']??null;
    if($systemDetails!==null&&(!is_string($systemDetails)||strlen($systemDetails)>4096||trim($systemDetails)===''))
        reply(['error'=>'invalid_system_details'],400);

    if($mode==='github')$displayName='';
    // Stable request IDs let the app retry a lost HTTP response without duplicating a report.
    $submissionId=$input['submissionId']??null;
    if($submissionId!==null&&(!is_string($submissionId)||!preg_match('/\A[a-f0-9]{32}\z/',$submissionId)))reply(['error'=>'invalid_submission_id'],400);
    $ticket=$submissionId===null?randomToken():hash_hmac('sha256','receipt:'.($participant??'').':'.$submissionId,$config['poll_secret']);
    $hashedTicket=hash('sha256',$ticket);$requestKey=$hashedTicket;
    $existing=feedbackFind($db,$hashedTicket);
    if($existing){
        if($existing['mode']!==$mode||$existing['title']!==$title||$existing['body']!==feedbackBody($mode,$body,$displayName,$existing['support_id']))reply(['error'=>'submission_conflict'],409);
        $stored=(bool)execute($db,'SELECT 1 FROM private_issue_logs WHERE ticket=?',[$hashedTicket])->fetchColumn();
        if($mode==='github'&&in_array($existing['status'],['awaiting_auth','needs_auth'],true)){
            if($signin===null)authorizationReply($db,$config,$existing,$ticket,$stored);
            execute($db,"UPDATE feedback_queue SET credential=?,credential_expires=?,status='queued',next_attempt=?,updated_at=? WHERE ticket=? AND status IN ('awaiting_auth','needs_auth')",[$signin['credential'],$signin['expires'],time(),time(),$hashedTicket]);
            $existing=feedbackDeliver($db,$config,$hashedTicket);
        }
        reply(feedbackPublic($existing)+['ticket'=>$ticket,'privateLogStored'=>$stored,'systemDetailsStored'=>feedbackSystemDetails($db,$hashedTicket)!==null],$existing['status']==='created'?200:202);
    }

    // Limit submissions per source and across the endpoint, including authorization attempts.
    $hour = intdiv(time(), 3600);
    $ip = $_SERVER['REMOTE_ADDR'] ?? '';
    $source = hash_hmac('sha256', "$hour:$ip", $config['client_secret']);
    $db->beginTransaction();
    execute($db, 'DELETE FROM rate_limit WHERE expires <= ?', [time()]);
    foreach ([$source => 3, "global:$hour" => 30] as $bucket => $limit) {
        $counter = execute($db, databaseSql($db, 'INSERT INTO rate_limit (bucket, count, expires) VALUES (?, 1, ?)
            ON CONFLICT(bucket) DO UPDATE SET count = count + 1 WHERE count < ?', 'INSERT INTO rate_limit (bucket, count, expires) VALUES (?, 1, ?)
            ON DUPLICATE KEY UPDATE count = IF(count < ?, count + 1, count)'),
            [$bucket, ($hour + 1) * 3600, $limit]);
        if ($counter->rowCount() === 0) {
            $db->rollBack();
            reply(['error' => 'rate_limit'], 429);
        }
    }
    $db->commit();

    $profile=$participant===null?null:supportProfile($db,$config,$participant,$mode==='anonymous'?$displayName:'');
    $body=feedbackBody($mode,$body,$displayName,$profile['supportId']??'');
    try{$report=feedbackStore($db,$hashedTicket,$requestKey,$mode,$title,$body,$profile,$displayName,$privateLog,$systemDetails);}
    catch(PDOException $error){
        $report=feedbackFind($db,$hashedTicket);if(!$report)throw $error;
    }
    if($mode==='github'&&$signin!==null){
        execute($db,"UPDATE feedback_queue SET credential=?,credential_expires=?,status='queued',next_attempt=?,updated_at=? WHERE ticket=? AND status IN ('awaiting_auth','needs_auth')",[$signin['credential'],$signin['expires'],time(),time(),$hashedTicket]);
    }elseif($mode==='github')authorizationReply($db,$config,$report,$ticket,$privateLog!==null);
    $report=feedbackDeliver($db,$config,$hashedTicket);
    reply(feedbackPublic($report)+['ticket'=>$ticket,'privateLogStored'=>$privateLog!==null,'systemDetailsStored'=>feedbackSystemDetails($db,$hashedTicket)!==null],$report['status']==='created'?201:202);
} catch (Throwable $error) {
    if (isset($db) && $db->inTransaction()) $db->rollBack();
    error_log('NxFileViewer feedback request failed (' . get_class($error) . ')');
    reply(['error' => 'service_unavailable'], 502);
}
