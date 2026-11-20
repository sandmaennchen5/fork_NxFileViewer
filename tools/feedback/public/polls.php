<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
require_once dirname(__DIR__).'/store/polls.php';
require_once dirname(__DIR__).'/store/support.php';
if(defined('NX_ADMIN_SECTION'))adminRequirePermission('view_polls');
if(!defined('NX_ADMIN_SECTION')&&isset($_GET['admin'])){header('Location: /admin/umfragen?lang='.webLanguage(),true,303);exit;}
header('Cache-Control: no-store');header('X-Content-Type-Options: nosniff');
header("Content-Security-Policy: default-src 'self'; style-src 'self' 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'");
ini_set('display_errors','0');
function jsonResponse(array $data,int $code=200): never {http_response_code($code);header('Content-Type: application/json; charset=utf-8');echo json_encode($data,JSON_THROW_ON_ERROR);exit;}
function escape(string $value): string {return htmlspecialchars($value,ENT_QUOTES,'UTF-8');}
$api=isset($_GET['api']);$admin=defined('NX_ADMIN_SECTION')&&NX_ADMIN_SECTION==='umfragen';$message='';$polls=[];$resultsOnly=isset($_GET['results'])||isset($_GET['image']);
try{
 $config=require dirname(__DIR__).'/store/config.php';$secret=$config['poll_secret']??'';if(strlen($secret)<32)throw new RuntimeException('Configure poll_secret');
 $db=databaseOpen($config);supportSchema($db);pollsSchema($db);
 $input=[];
 if($api){
  if(($_SERVER['REQUEST_METHOD']??'GET')==='POST'){$raw=file_get_contents('php://input',false,null,0,32769);if($raw===false||strlen($raw)>32768)jsonResponse(['error'=>'Request too large'],413);$input=json_decode($raw,true,flags:JSON_THROW_ON_ERROR);}
  elseif(($_SERVER['REQUEST_METHOD']??'GET')!=='GET')jsonResponse(['error'=>'Method not allowed'],405);
  $identity=$input['participant']??$_GET['participant']??'';if(!is_string($identity)||!preg_match('/\A[a-f0-9]{64}\z/',$identity))jsonResponse(['error'=>'Invalid participant'],400);
  $participant=supportInternalId($config,$identity);
 }else{
  if(session_status()!==PHP_SESSION_ACTIVE){session_set_cookie_params(['secure'=>true,'httponly'=>true,'samesite'=>'Lax']);session_start();}
  $_SESSION['participant']??=bin2hex(random_bytes(32));$_SESSION['csrf']??=bin2hex(random_bytes(32));$participant=hash_hmac('sha256',$_SESSION['participant'],$secret);
 }
 $bucket=hash_hmac('sha256',intdiv(time(),3600).':'.($_SERVER['REMOTE_ADDR']??''),$secret);
 pollQuery($db,'DELETE FROM poll_rate WHERE expires<?',[time()]);
 if(($_SERVER['REQUEST_METHOD']??'GET')==='POST'){
  if($api){$action=(string)($input['action']??'vote');pollRespond($db,(int)($input['poll']??0),$participant,$bucket,$action,isset($input['option'])?(int)$input['option']:null,isset($input['answer'])?(string)$input['answer']:null,true);}
  else{
   if(!is_string($_POST['csrf']??null)||!hash_equals($_SESSION['csrf'],$_POST['csrf']))throw new DomainException('Bitte die Seite neu laden.');
   $action=(string)($_POST['action']??'');$pollId=(int)($_POST['poll']??0);
   if(in_array($action,['vote','text','skip','unskip'],true))pollRespond($db,$pollId,$participant,$bucket,$action,isset($_POST['option'])?(int)$_POST['option']:null,isset($_POST['answer'])?(string)$_POST['answer']:null);
   elseif(in_array($action,['create','edit','translations','close','delete'],true)){
    if(!$admin||empty($_SESSION['poll_admin']))throw new DomainException('Bitte anmelden.');adminRequirePermission($action==='delete'?'delete_polls':'manage_polls');
    if($action==='create')pollCreate($db,$_POST);
    elseif($action==='edit')pollEdit($db,$pollId,$_POST);
    elseif($action==='delete'){if(($_POST['confirm_delete']??'')!=='yes')throw new DomainException('Bitte das endgültige Löschen bestätigen.');pollDelete($db,$pollId);}
    elseif($action==='close')pollQuery($db,'UPDATE polls SET closed=1 WHERE id=?',[$pollId]);
    else{
     $count=(int)pollQuery($db,'SELECT COUNT(*) FROM poll_options WHERE poll_id=?',[$pollId])->fetchColumn();
     $english=pollEnglishInput((string)($_POST['question_en']??''),(string)($_POST['options_en']??''),$count);
     $db->beginTransaction();pollLock($db,$pollId);pollSaveEnglish($db,$pollId,$english);$db->commit();
    }
   }else throw new DomainException('Unbekannte Aktion.');
   header('Location: '.($admin?'/admin/umfragen':'/umfragen').'?lang='.webLanguage(),true,303);exit;
  }
 }
 $polls=pollsOverview($db,$participant,webLanguage(),$api);
 if(isset($_GET['poll'])){$id=(int)$_GET['poll'];$polls=array_values(array_filter($polls,fn($poll)=>$poll['id']===$id));if(!$polls){if($api)jsonResponse(['error'=>webT('Umfrage nicht gefunden.')],404);http_response_code(404);$message=webT('Umfrage nicht gefunden.');}}
 if($api)jsonResponse(['polls'=>$polls]);
 if(isset($_GET['image'])){
  if(count($polls)!==1)jsonResponse(['error'=>webT('Umfrage nicht gefunden.')],404);
  $poll=$polls[0];$height=140+count($poll['options'])*62;$total=array_sum(array_column($poll['options'],'votes'));
  $xml=fn($s)=>htmlspecialchars((string)$s,ENT_QUOTES|ENT_XML1,'UTF-8');header('Content-Type: image/svg+xml; charset=utf-8');
  echo '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="'.$height.'" viewBox="0 0 800 '.$height.'"><rect width="800" height="'.$height.'" fill="#18202b"/><defs><clipPath id="title"><rect x="24" y="0" width="752" height="65"/></clipPath><clipPath id="labels"><rect x="24" y="100" width="640" height="'.$height.'"/></clipPath></defs>';
  echo '<text x="24" y="46" fill="white" font-family="sans-serif" font-size="20" clip-path="url(#title)"><title>'.$xml($poll['question']).'</title>'.$xml($poll['question']).'</text>';
  echo '<text x="24" y="90" fill="#b9c7d7" font-family="sans-serif" font-size="14">'.($poll['kind']==='text'?$poll['responseCount'].' '.$xml(webT('Freitext-Antworten')):$total.' '.$xml(webT('Stimmen'))).' · '.$xml(webT($poll['closed']?'Abgeschlossen':'Offen')).'</text>';
  foreach($poll['options'] as $index=>$option){$y=120+$index*62;$width=(int)round(640*$option['votes']/max(1,$total));$label=$option['label'].($option['active']?'':' ('.webT('Entfernt').')');
   echo '<text x="24" y="'.$y.'" fill="white" font-family="sans-serif" font-size="14" clip-path="url(#labels)">'.$xml($label).'</text><rect x="24" y="'.($y+9).'" width="640" height="12" rx="6" fill="#344255"/><rect x="24" y="'.($y+9).'" width="'.$width.'" height="12" rx="6" fill="#58b5ff"/><text x="686" y="'.($y+20).'" fill="white" font-family="sans-serif" font-size="14">'.$option['votes'].'</text>';
  }echo '</svg>';exit;
 }
}catch(Throwable $e){if(isset($db)&&$db->inTransaction())$db->rollBack();$message=webT($e instanceof DomainException?$e->getMessage():'Der Umfragedienst ist derzeit nicht verfügbar.');if($api)jsonResponse(['error'=>$message],$e instanceof DomainException?400:503);http_response_code($e instanceof DomainException?400:503);}
function pollCsrf(): string {return '<input type="hidden" name="csrf" value="'.escape($_SESSION['csrf']??'').'">';}
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title><?=webT('NxFileViewer Umfragen')?></title>
<?php if($resultsOnly):?><meta http-equiv="refresh" content="30"><?php endif ?>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{max-width:900px;margin:40px auto;padding:0 20px;line-height:1.6}article,fieldset{border:1px solid #8886;border-radius:12px;padding:20px;margin:20px 0}h1{font-size:1.8rem}h2{font-size:1.2rem}label{display:block;margin:12px 0}input[type=text],textarea{box-sizing:border-box;width:100%;padding:10px;font:inherit}button,select{padding:9px;font:inherit;cursor:pointer}progress{width:100%}.retired{opacity:.7}small{opacity:.85}.answer{white-space:pre-wrap;overflow-wrap:anywhere}</style>
<h1><?=webT('NxFileViewer Umfragen')?></h1><?php if($admin)adminNavigation('umfragen');else webLanguageSelector(); ?>
<?php if($message!==''):?><p role="alert"><?=escape($message)?></p><?php endif ?>
<?php if($admin): require __DIR__.'/admin-polls.php';else: ?>
<p><?=webT('Stimme für die Weiterentwicklung von NxFileViewer ab.')?></p>
<?php foreach($polls as $poll):$total=array_sum(array_column($poll['options'],'votes')); ?>
<article><h2><?=escape($poll['question'])?></h2><p><?=webT($poll['closed']?'Abgeschlossen':'Offen')?></p>
<p><a href="/umfragen?poll=<?=$poll['id']?>&amp;results=1&amp;lang=<?=webLanguage()?>"><?=webT('Ergebnislink')?></a> · <a href="/umfragen?poll=<?=$poll['id']?>&amp;image=1&amp;lang=<?=webLanguage()?>"><?=webT('Aktuelles Ergebnisbild (SVG)')?></a></p>
<?php if($poll['declined']):?><p><?=webT('Du nimmst an dieser Umfrage nicht teil.')?></p><?php elseif($poll['hasResponded']):?><p><?=webT('Deine Antwort wurde gespeichert.')?></p><?php endif ?>
<form method="post"><?=pollCsrf()?><input type="hidden" name="poll" value="<?=$poll['id']?>">
<?php if($poll['kind']==='text'):?>
<p><?=webT('Freitext-Antworten sind nur im Adminbereich sichtbar.')?></p><p><?=$poll['responseCount']?> <?=webT('Freitext-Antworten')?></p>
<?php if($poll['responseText']!==null):?><div class="answer"><?=escape($poll['responseText'])?></div><?php elseif($poll['canParticipate']&&!$resultsOnly):?><label><?=webT('Deine Antwort')?><textarea name="answer" maxlength="2000" rows="5" required></textarea></label><button name="action" value="text"><?=webT('Antwort senden')?></button><?php endif ?>
<?php else:foreach($poll['options'] as $option):?>
<label class="<?=$option['active']?'':'retired'?>"><?php if($poll['canParticipate']&&!$resultsOnly&&$option['active']):?><input type="radio" name="option" value="<?=$option['id']?>" required><?php endif ?>
<?=escape($option['label'])?><?=$option['active']?'':' ('.webT('Entfernt').')'?> · <?=$option['votes']?> <?=webT('Stimmen')?><?=$poll['selectedOption']===$option['id']?' · '.webT('Deine Stimme'):''?><progress value="<?=$option['votes']?>" max="<?=max(1,$total)?>"></progress></label>
<?php endforeach;if($poll['canParticipate']&&!$resultsOnly):?><button name="action" value="vote"><?=webT('Abstimmen')?></button><?php endif;endif ?></form>
<?php if(!$resultsOnly&&!$poll['closed']&&($poll['canParticipate']||$poll['declined'])):?><form method="post"><?=pollCsrf()?><input type="hidden" name="poll" value="<?=$poll['id']?>"><button name="action" value="<?=$poll['declined']?'unskip':'skip'?>"><?=webT($poll['declined']?'Doch teilnehmen':'Nicht teilnehmen')?></button></form><?php endif ?>
</article><?php endforeach;if(!$polls&&$message===''):?><p><?=webT('Zurzeit gibt es keine Umfragen.')?></p><?php endif ?>
<p><small><?=webT('Stimmen werden pro Teilnehmerkennung gezählt. Programm und Browser verwenden eigene Kennungen.')?></small></p><?php endif ?></html>
