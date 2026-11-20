<?php
if(!defined('NX_ADMIN_SECTION')||NX_ADMIN_SECTION!=='umfragen'){http_response_code(403);exit;}
adminRequirePermission('view_polls');
function pollSettings(array $poll=[]): void { ?>
<input type="hidden" name="visibility_form" value="1">
<?php foreach(['show_in_app'=>'Umfrage in der App zur Teilnahme anzeigen','show_results_in_app'=>'Ergebnisse in der App anzeigen','notify_in_app'=>'Beim App-Start an unbeantwortete Umfrage erinnern'] as $key=>$label):?><label><input type="checkbox" name="<?=$key?>" value="1" <?=($poll[$key]??true)?'checked':''?>><?=webT($label)?></label><?php endforeach ?>
<p><small><?=webT('Ohne Start-Hinweis ist die Umfrage optional und weiterhin unter Umfragen erreichbar. Beide App-Anzeigen ausschalten blendet sie dort vollständig aus. Die öffentliche Webseite bleibt erreichbar.')?></small></p>
<?php }
if(adminCan('manage_polls')): ?>
<details><summary><?=webT('Neue Umfrage erstellen')?></summary><form method="post"><?=pollCsrf()?><input type="hidden" name="action" value="create">
<label><?=webT('Frage (Deutsch)')?><input type="text" name="question" maxlength="250" required></label>
<label><?=webT('Frage (English, optional)')?><input type="text" name="question_en" maxlength="250"></label>
<label><?=webT('Antwortart')?><select name="kind"><option value="choice"><?=webT('Auswahl')?></option><option value="text"><?=webT('Freitext')?></option></select></label>
<label><?=webT('Antworten (2 bis 20), eine je Zeile')?><textarea name="options" rows="5" maxlength="10020"></textarea></label>
<label><?=webT('Antworten (English, gleiche Reihenfolge)')?><textarea name="options_en" rows="5" maxlength="10020"></textarea></label>
<p><?=webT('Bei Freitext die Antwortlisten leer lassen. Freitext-Antworten werden nur dem Admin angezeigt.')?></p>
<?php pollSettings(); ?><button><?=webT('Umfrage veröffentlichen')?></button></form></details>
<?php endif;foreach($polls as $poll):
$id=$poll['id'];$raw=pollQuery($db,'SELECT * FROM polls WHERE id=?',[$id])->fetch(PDO::FETCH_ASSOC);
$enQuestion=pollQuery($db,"SELECT question FROM poll_translations WHERE poll_id=? AND language='en'",[$id])->fetchColumn();
$rawOptions=pollQuery($db,"SELECT o.*,COALESCE(t.label,'') AS english FROM poll_options o LEFT JOIN poll_option_translations t ON t.option_id=o.id AND t.language='en' WHERE o.poll_id=? ORDER BY o.position,o.id",[$id])->fetchAll(PDO::FETCH_ASSOC);
?><article><h2><?=escape($poll['question'])?></h2><p><?=webT($poll['kind']==='text'?'Freitext':'Auswahl')?> · <?=webT($poll['closed']?'Abgeschlossen':'Offen')?></p>
<p><a href="/umfragen?poll=<?=$id?>&amp;results=1&amp;lang=<?=webLanguage()?>"><?=webT('Ergebnislink')?></a> · <a href="/umfragen?poll=<?=$id?>&amp;image=1&amp;lang=<?=webLanguage()?>"><?=webT('Aktuelles Ergebnisbild (SVG)')?></a></p>
<?php if($poll['kind']==='choice'):?><ul><?php foreach($poll['options'] as $option):?><li><?=escape($option['label'])?><?=$option['active']?'':' ('.webT('Entfernt').')'?> · <?=$option['votes']?> <?=webT('Stimmen')?></li><?php endforeach ?></ul>
<?php else:?><p><?=$poll['responseCount']?> <?=webT('Freitext-Antworten')?></p>
<?php $answers=pollQuery($db,"SELECT r.answer,r.created_at,p.support_id FROM poll_responses r LEFT JOIN support_profiles p ON p.internal_id=r.participant WHERE r.poll_id=? AND r.kind='text' ORDER BY r.created_at DESC,r.participant LIMIT 200",[$id])->fetchAll(PDO::FETCH_ASSOC);foreach($answers as $answer):?><blockquote><p class="answer"><?=escape($answer['answer'])?></p><small><?=gmdate('Y-m-d H:i:s',(int)$answer['created_at'])?> UTC · <?=escape($answer['support_id']??'Browser')?></small></blockquote><?php endforeach;endif ?>
<?php if(adminCan('manage_polls')):?>
<details><summary><?=webT('Umfrage bearbeiten')?></summary>
<p><?=webT('Vorhandene Antwort-IDs und Stimmen bleiben erhalten. Abgewählte Antworten können nicht mehr gewählt werden; ihre bisherigen Stimmen bleiben im Ergebnis sichtbar. Texte nur ändern, wenn die Bedeutung erhalten bleibt.')?></p>
<form method="post"><?=pollCsrf()?><input type="hidden" name="action" value="edit"><input type="hidden" name="poll" value="<?=$id?>">
<label><?=webT('Frage (Deutsch)')?><input type="text" name="question" value="<?=escape($raw['question'])?>" maxlength="250" required></label>
<label><?=webT('Frage (English, optional)')?><input type="text" name="question_en" value="<?=escape($enQuestion?:'')?>" maxlength="250"></label>
<?php foreach($rawOptions as $option):$oid=(int)$option['id'];?><fieldset><legend>#<?=$oid?> · <?=webT('Vorhandene Auswahl')?></legend>
<label><input type="checkbox" name="option_active[<?=$oid?>]" value="1" <?=$option['active']?'checked':''?>><?=webT('Weiter auswählbar')?></label>
<label>Deutsch<input type="text" name="option_de[<?=$oid?>]" value="<?=escape($option['label'])?>" maxlength="125" required></label>
<label>English<input type="text" name="option_en[<?=$oid?>]" value="<?=escape($option['english'])?>" maxlength="125"></label></fieldset><?php endforeach ?>
<?php if($poll['kind']==='choice'):?><label><?=webT('Zusätzliche Antworten (Deutsch), eine je Zeile')?><textarea name="new_options" maxlength="10020" rows="3"></textarea></label><label><?=webT('Zusätzliche Antworten (English), gleiche Reihenfolge')?><textarea name="new_options_en" maxlength="10020" rows="3"></textarea></label><?php endif ?>
<?php pollSettings($raw); ?><label><input type="checkbox" name="closed" value="1" <?=$poll['closed']?'checked':''?>><?=webT('Umfrage geschlossen')?></label>
<button><?=webT('Änderungen speichern')?></button></form></details>
<?php endif;if(adminCan('delete_polls')):?><form method="post"><?=pollCsrf()?><input type="hidden" name="poll" value="<?=$id?>"><label><input type="checkbox" name="confirm_delete" value="yes" required><?=webT('Diese Umfrage und alle Stimmen endgültig löschen')?></label><button name="action" value="delete"><?=webT('Umfrage löschen')?></button></form><?php endif ?></article>
<?php endforeach ?>
