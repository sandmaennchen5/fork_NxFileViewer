<?php
declare(strict_types=1);
header('Cache-Control: no-store');header('Content-Type: application/json; charset=utf-8');header('X-Content-Type-Options: nosniff');
if(!in_array(strtolower((string)($_SERVER['HTTPS']??'')),['on','1'],true)){http_response_code(426);exit;}
// Use the cron service's Basic-Auth fields; never put this secret in a query string.
try{$config=require dirname(__DIR__).'/store/config.php';}catch(Throwable $e){http_response_code(503);exit;}
$secret=$config['queue_cron_secret']??'';$password=$_SERVER['PHP_AUTH_PW']??'';$username=$_SERVER['PHP_AUTH_USER']??'';
if($username===''){
 $header=$_SERVER['HTTP_AUTHORIZATION']??$_SERVER['REDIRECT_HTTP_AUTHORIZATION']??'';
 if(is_string($header)&&strlen($header)<=1024&&str_starts_with($header,'Basic ')){
  $decoded=base64_decode(substr($header,6),true);
  if(is_string($decoded)&&str_contains($decoded,':'))[$username,$password]=explode(':',$decoded,2);
 }
}
if(!is_string($secret)||strlen($secret)<32||$username!=='queue'||!is_string($password)||!hash_equals($secret,$password)){
 http_response_code(401);header('WWW-Authenticate: Basic realm="Feedback cron", charset="UTF-8"');exit;
}
if(isset($_SERVER['HTTP_SEC_FETCH_SITE'])){http_response_code(403);exit;}
if(!in_array($_SERVER['REQUEST_METHOD']??'',['GET','POST'],true)){http_response_code(405);exit;}
try{
 require_once dirname(__DIR__).'/store/feedback-queue.php';$db=databaseOpen($config);supportSchema($db);privateLogSchema($db);
 echo json_encode(['ok'=>true,'processed'=>feedbackRun($db,$config)],JSON_THROW_ON_ERROR);
}catch(Throwable $e){http_response_code(503);echo '{"ok":false}';}
