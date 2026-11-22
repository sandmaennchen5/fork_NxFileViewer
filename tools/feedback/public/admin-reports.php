<?php
declare(strict_types=1);
require_once dirname(__DIR__).'/store/web-language.php';
if(!defined('NX_ADMIN_SECTION')||NX_ADMIN_SECTION!=='meldungen'){http_response_code(403);exit;}
adminRequirePermission('view_support');
require_once dirname(__DIR__).'/store/feedback-queue.php';
require_once dirname(__DIR__).'/store/github-status.php';
feedbackSchema($db);privateLogSchema($db);supportSchema($db);
$labels=['awaiting_auth'=>webT('Wartet auf GitHub-Anmeldung'),'needs_auth'=>webT('GitHub-Anmeldung erforderlich'),'queued'=>webT('Wartet auf Übertragung'),'preparing'=>webT('Übertragung wird vorbereitet'),'sending'=>webT('Wird an GitHub gesendet'),'created'=>webT('Erfolgreich übertragen'),'review'=>webT('Erstellung unklar – prüfen'),'blocked'=>webT('Übertragung fehlgeschlagen')];
$error='';$notice='';$selected=null;
try{
 if(($_SERVER['REQUEST_METHOD']??'')==='POST'){
  if(!is_string($_POST['csrf']??null)||!hash_equals($_SESSION['csrf'],$_POST['csrf']))throw new DomainException(webT('Bitte Seite neu laden.'));
  $action=(string)($_POST['action']??'');$ticket=(string)($_POST['ticket']??'');$report=feedbackFind($db,$ticket);
  if(!$report)throw new DomainException(webT('Meldung nicht gefunden.'));
  if($action==='delete-report-log'){
   adminRequirePermission('delete_logs');feedbackQuery($db,'DELETE FROM private_issue_logs WHERE ticket=?',[$ticket]);$notice=webT('Privates Log gelöscht.');
  }else{
   adminRequirePermission('manage_reports');
   if($action==='retry-report'){
    if(!in_array($report['status'],['queued','review','blocked'],true))throw new DomainException(webT('Diese Meldung kann aktuell nicht erneut gesendet werden.'));
    if($report['status']==='review'&&($_POST['confirm_duplicate']??'')!=='yes')throw new DomainException(webT('Vorher GitHub prüfen und das mögliche Duplikat bestätigen.'));
    if($report['mode']==='github'&&($report['credential']===null||(int)$report['credential_expires']<=time()+30))throw new DomainException(webT('Der Benutzer muss GitHub erneut autorisieren. Veröffentlichung ohne sein Konto erfolgt nicht.'));
    feedbackQuery($db,"UPDATE feedback_queue SET status='queued',next_attempt=?,last_error='',updated_at=? WHERE ticket=? AND status IN ('queued','review','blocked')",[time(),time(),$ticket]);
    $updated=feedbackDeliver($db,$config,$ticket);$notice='Status: '.($labels[$updated['status']]??$updated['status']);
   }elseif($action==='link-report'){
    if(!in_array($report['status'],['review','blocked'],true))throw new DomainException(webT('Nur unklare / fehlgeschlagene Meldungen können zugeordnet werden.'));
    $number=filter_var($_POST['number']??null,FILTER_VALIDATE_INT,['options'=>['min_range'=>1]]);if(!$number)throw new DomainException(webT('Issue-Nummer fehlt.'));
    $issue=supportGithubGet('https://api.github.com/repos/'.FEEDBACK_REPOSITORY.'/issues/'.$number,installationToken($config));
    if(isset($issue['pull_request'])||!str_contains((string)($issue['body']??''),feedbackMarker($ticket)))throw new DomainException(webT('Dieses Issue enthält nicht die Kennung dieser Meldung.'));
    feedbackComplete($db,$report,$number,(string)($issue['user']['login']??''));$notice=webT('Vorhandenes GitHub-Issue zugeordnet.');
   }else throw new DomainException(webT('Ungültige Aktion.'));
  }
 }
 $filter=(string)($_GET['status']??'');if($filter!==''&&!isset($labels[$filter]))throw new DomainException(webT('Ungültiger Status.'));
 $search=trim((string)($_GET['q']??''));if(strlen($search)>200)throw new DomainException(webT('Suchtext zu lang.'));
 $page=max(1,(int)($_GET['page']??1));$where=[];$args=[];
 if($filter!==''){$where[]='status=?';$args[]=$filter;}
 if($search!==''){$where[]="(title LIKE ? OR support_id LIKE ? OR display_name LIKE ? OR ticket LIKE ?)";for($i=0;$i<4;$i++)$args[]='%'.$search.'%';}
 $sql=$where?' WHERE '.implode(' AND ',$where):'';$count=(int)feedbackQuery($db,'SELECT COUNT(*) FROM feedback_queue'.$sql,$args)->fetchColumn();$page=min($page,max(1,(int)ceil($count/50)));
 $rows=feedbackQuery($db,'SELECT ticket,title,status,support_id,created_at,number FROM feedback_queue'.$sql.' ORDER BY created_at DESC,ticket LIMIT 50 OFFSET '.(($page-1)*50),$args)->fetchAll(PDO::FETCH_ASSOC);
 $id=(string)($_GET['id']??($_POST['ticket']??''));if($id!=='')$selected=feedbackFind($db,$id);
}catch(Throwable $e){http_response_code($e instanceof DomainException?400:503);$error=$e instanceof DomainException?$e->getMessage():webT('Aktion derzeit nicht möglich. Gespeicherte Meldungen bleiben erhalten.');}
?>
<!doctype html><html lang="<?=webLanguage()?>"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title><?=webT('Meldungsarchiv')?></title>
<style>:root{color-scheme:light dark;font-family:system-ui,sans-serif}body{max-width:1100px;margin:30px auto;padding:0 20px;line-height:1.6}article{border:1px solid #8886;border-radius:12px;padding:20px;margin:20px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:8px;border-bottom:1px solid #8885}input,select,button{padding:8px;font:inherit}label{display:block;margin:12px 0}</style>
<h1><?=webT('Meldungsarchiv')?></h1><?php adminNavigation('meldungen'); ?>
<p><?=webT('Der gespeicherte Inhalt ist unabhängig von GitHub verfügbar. Die Liste zeigt den Übertragungsstatus; spätere Änderungen auf GitHub ändern diesen gespeicherten Stand nicht.')?></p>
<?php if($error!==''):?><p role="alert"><?=adminEscape(webT($error))?></p><?php endif ?>
<?php if($notice!==''):?><p role="status"><?=adminEscape($notice)?></p><?php endif ?>
<?php if($selected):?><article><h2><?=adminEscape($selected['title'])?></h2>
<p><?=adminEscape($labels[$selected['status']]??$selected['status'])?><?=webT(' · Eingang ')?><?=gmdate('Y-m-d H:i:s',(int)$selected['created_at'])?><?=webT(' UTC · Versuche ')?><?=(int)$selected['attempts']?></p>
<p>Support-ID: <?=adminEscape($selected['support_id'])?> · Name: <?=adminEscape($selected['display_name'])?> · GitHub: <?=adminEscape($selected['author'])?><?=webT(' · Modus: ')?><?=adminEscape($selected['mode'])?></p>
<?php if($selected['last_error']!==''):?><p><?=webT('Hinweis: ')?><?=adminEscape([
'github_credentials_unavailable'=>webT('GitHub-Zugang derzeit nicht verfügbar. Automatischer Wiederholungsversuch mit Wartezeit.'),
'github_rejected_temporarily'=>webT('GitHub hat die Anfrage vorübergehend abgelehnt. Automatischer Wiederholungsversuch mit Wartezeit.'),
'delivery_unconfirmed'=>webT('GitHub könnte das Issue bereits angelegt haben. Vor erneutem Senden prüfen und gegebenenfalls ein vorhandenes Issue zuordnen.'),
'authorization_required'=>webT('Der Benutzer muss die GitHub-Anmeldung erneut bestätigen.'),
'authorization_unavailable'=>webT('GitHub-Anmeldung konnte nicht abgeschlossen werden. Die Meldung bleibt hier gespeichert.'),
'github_rejected'=>webT('GitHub hat die Erstellung abgelehnt. Konfiguration und Meldung prüfen.')
][$selected['last_error']]??webT('Übertragung prüfen.'))?></p><?php endif ?>
<?php if($selected['status']==='queued'):?><p><?=webT('Nächster Versuch frühestens ')?><?=gmdate('Y-m-d H:i:s',(int)$selected['next_attempt'])?><?=webT(' UTC (Cronjob erforderlich).')?></p><?php endif ?>
<p><?=webT('Meldungskennung: ')?><code><?=adminEscape($selected['ticket'])?></code></p>
<?php if($selected['number']):?><p><a href="https://github.com/<?=FEEDBACK_REPOSITORY?>/issues/<?=(int)$selected['number']?>">GitHub-Issue #<?=(int)$selected['number']?></a></p><?php endif ?>
<pre><?=adminEscape($selected['body'])?></pre>
<?php $systemDetails=feedbackSystemDetails($db,$selected['ticket']);if($systemDetails!==null):?>
<h3><?=webT('Systemdaten (privat)')?></h3>
<pre><?=adminEscape($systemDetails)?></pre>
<?php endif ?>
<?php $hasLog=feedbackQuery($db,'SELECT 1 FROM private_issue_logs WHERE ticket=?',[$selected['ticket']])->fetchColumn();if($hasLog):?>
<?php if(adminCan('download_logs')):?><p><a href="/admin/support/log?report=<?=adminEscape($selected['ticket'])?><?=webT('">Original-Log herunterladen')?></a></p><?php endif ?>
<?php if(adminCan('delete_logs')):?><form method="post"><?=adminCsrf()?><input type="hidden" name="ticket" value="<?=adminEscape($selected['ticket'])?>"><button name="action" value="delete-report-log"><?=webT('Privates Log löschen')?></button></form><?php endif ?>
<?php endif ?>
<?php if(adminCan('manage_reports')&&in_array($selected['status'],['queued','review','blocked'],true)):?>
<form method="post"><?=adminCsrf()?><input type="hidden" name="ticket" value="<?=adminEscape($selected['ticket'])?>">
<?php if($selected['status']==='review'):?><label><input type="checkbox" name="confirm_duplicate" value="yes" required><?=webT('Ich habe GitHub geprüft. Erneutes Senden könnte ein doppeltes Issue erzeugen.')?></label><?php endif ?>
<button name="action" value="retry-report"><?=webT('Übertragung jetzt versuchen')?></button></form>
<?php endif ?>
<?php if(adminCan('manage_reports')&&in_array($selected['status'],['review','blocked'],true)):?>
<form method="post"><?=adminCsrf()?><input type="hidden" name="ticket" value="<?=adminEscape($selected['ticket'])?>"><label><?=webT('Bereits angelegtes GitHub-Issue ')?><input type="number" min="1" name="number" required></label><button name="action" value="link-report"><?=webT('Prüfen und zuordnen')?></button></form>
<?php endif ?></article><?php endif ?>
<form method="get"><label>Status <select name="status"><option value=""><?=webT('Alle')?></option><?php foreach($labels as $key=>$text):?><option value="<?=$key?>" <?=($filter??'')===$key?'selected':''?>><?=adminEscape($text)?></option><?php endforeach ?></select></label><label><?=webT('Suche <input name="q" value="')?><?=adminEscape($search??'')?>"></label><button><?=webT('Filtern')?></button></form>
<p><?=(int)($count??0)?><?=webT(' Meldungen')?></p><table><tr><th><?=webT('Eingang (UTC)')?></th><th><?=webT('Titel')?></th><th>Status</th><th>Support-ID</th></tr>
<?php foreach($rows??[] as $row):?><tr><td><?=gmdate('Y-m-d H:i',(int)$row['created_at'])?></td><td><a href="?<?=adminEscape(http_build_query(['id'=>$row['ticket'],'status'=>$filter??'','q'=>$search??'','page'=>$page??1]))?>"><?=adminEscape($row['title'])?></a></td><td><?=adminEscape($labels[$row['status']]??$row['status'])?></td><td><?=adminEscape($row['support_id'])?></td></tr><?php endforeach ?></table>
<?php if(($page??1)>1):?><a href="?<?=adminEscape(http_build_query(['status'=>$filter,'q'=>$search,'page'=>$page-1]))?><?=webT('">Zurück')?></a><?php endif ?>
<?php if(($page??1)*50<($count??0)):?><a href="?<?=adminEscape(http_build_query(['status'=>$filter,'q'=>$search,'page'=>$page+1]))?><?=webT('">Weiter')?></a><?php endif ?></html>
