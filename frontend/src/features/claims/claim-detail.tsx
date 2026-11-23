'use client';

import type {
  ClaimAttachment,
  ClaimAttachmentPayload,
  ClaimDetail,
  ClaimStatus,
  UserListItem,
} from '@delivery/shared';
import {
  AlertTriangle,
  CalendarClock,
  Loader2,
  Lock,
  Package,
  Paperclip,
  Send,
  UserCog,
} from 'lucide-react';
import { useFormatter, useTranslations } from 'next-intl';
import { useState, useTransition } from 'react';
import { toast } from 'sonner';
import { SearchableSelect } from '@/components/searchable-select';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { AttachmentPicker } from '@/features/claims/attachment-picker';
import { ClaimStatusBadge } from '@/features/claims/status-badge';
import { Link } from '@/i18n/navigation';
import { assignClaimAction, claimAttachmentAction, replyClaimAction } from '@/lib/actions/claims';
import { downloadBase64 } from '@/lib/download';
import { cn } from '@/lib/utils';

/** Sentinelle : Radix Select interdit une valeur vide. */
const UNASSIGNED = '__none__';

/**
 * Fiche d'une réclamation — l'exposé, le fil, et la réponse.
 *
 * Le fil est APPEND-ONLY : rien ne s'y modifie ni ne s'y supprime, et l'écran
 * n'offre donc aucun geste pour le faire. C'est ce que le legacy n'avait pas —
 * `delete_traking` effaçait physiquement une ligne d'historique (§4 dette n°8).
 *
 * Les notes d'agence ne sont pas masquées à l'affichage : l'API ne les envoie
 * pas du tout à l'e-commerçant. Ce qui ne part pas sur le réseau ne fuit pas.
 */
export function ClaimDetailView({
  claim,
  team,
  canHandle,
}: {
  claim: ClaimDetail;
  /** Agents assignables. Vide pour un e-commerçant, qui n'assigne personne. */
  team: UserListItem[];
  canHandle: boolean;
}) {
  const t = useTranslations('claims.detail');
  const tStatus = useTranslations('claims.status');
  const format = useFormatter();

  const [body, setBody] = useState('');
  const [status, setStatus] = useState<ClaimStatus | ''>('');
  const [isInternal, setIsInternal] = useState(false);
  const [attachments, setAttachments] = useState<ClaimAttachmentPayload[]>([]);
  const [pending, startTransition] = useTransition();

  const send = () => {
    if (body.trim() === '' && status === '' && attachments.length === 0) {
      toast.error(t('emptyReply'));
      return;
    }

    startTransition(async () => {
      const result = await replyClaimAction(claim.code, {
        body: body.trim() || undefined,
        status: status || undefined,
        isInternal,
        attachments,
      });

      if (!result.ok) {
        toast.error(result.error);
        return;
      }

      setBody('');
      setStatus('');
      setIsInternal(false);
      setAttachments([]);
      toast.success(t('sent'));
    });
  };

  const assign = (value: string) => {
    startTransition(async () => {
      const result = await assignClaimAction(claim.code, {
        assigneeId: value === UNASSIGNED ? null : value,
      });

      if (!result.ok) toast.error(result.error);
    });
  };

  return (
    <div className="flex flex-col gap-4 lg:grid lg:grid-cols-3 lg:items-start">
      <div className="flex flex-col gap-4 lg:col-span-2">
        {/* L'exposé initial : immuable, et distinct du fil. */}
        <Card>
          <CardContent className="flex flex-col gap-3 pt-6">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <h2 className="text-lg font-semibold tracking-tight">{claim.subject}</h2>
                <p className="text-muted-foreground mt-0.5 text-sm">
                  {claim.typeLabel} · {claim.clientName}
                </p>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <ClaimStatusBadge status={claim.status} />
                {claim.isLate ? (
                  <span className="text-destructive inline-flex items-center gap-1 text-xs font-medium">
                    <AlertTriangle className="size-3.5" aria-hidden />
                    {t('late')}
                  </span>
                ) : null}
              </div>
            </div>

            <p className="text-sm whitespace-pre-wrap">{claim.message}</p>

            <AttachmentList claim={claim} attachments={claim.attachments} />

            <p className="text-muted-foreground text-xs">
              {t('openedBy', {
                author: claim.authorName,
                date: format.dateTime(new Date(claim.createdAt), 'stamp'),
              })}
            </p>
          </CardContent>
        </Card>

        {/* Le fil */}
        <section className="flex flex-col gap-3">
          <h3 className="text-sm font-semibold">{t('thread')}</h3>

          {claim.events.map((event) => (
            <Card
              key={event.id}
              className={cn(event.isInternal && 'border-dashed bg-[var(--muted)]/40')}
            >
              <CardContent className="flex flex-col gap-2 pt-5">
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="font-medium">{event.authorName}</span>
                  <span className="text-muted-foreground">
                    {format.dateTime(new Date(event.createdAt), 'stamp')}
                  </span>
                  {event.status ? (
                    <span className="text-muted-foreground">
                      · {t('movedTo', { status: tStatus(event.status) })}
                    </span>
                  ) : null}
                  {event.isInternal ? (
                    <span className="text-muted-foreground inline-flex items-center gap-1">
                      <Lock className="size-3" aria-hidden />
                      {t('internal')}
                    </span>
                  ) : null}
                </div>

                {event.body ? <p className="text-sm whitespace-pre-wrap">{event.body}</p> : null}

                <AttachmentList claim={claim} attachments={event.attachments} />
              </CardContent>
            </Card>
          ))}
        </section>

        {/* La réponse */}
        <Card>
          <CardContent className="flex flex-col gap-3 pt-6">
            <Label htmlFor="claim-reply">{t('reply')}</Label>
            <Textarea
              id="claim-reply"
              rows={4}
              value={body}
              onChange={(event) => setBody(event.target.value)}
              placeholder={t('replyPlaceholder')}
              disabled={pending}
            />

            <AttachmentPicker value={attachments} onChange={setAttachments} disabled={pending} />

            {canHandle ? (
              <div className="flex flex-col gap-3 border-t pt-3 sm:flex-row sm:items-end">
                <div className="flex flex-1 flex-col gap-1.5">
                  <Label htmlFor="claim-status">{t('changeStatus')}</Label>
                  <Select
                    value={status}
                    onValueChange={(value) => setStatus(value as ClaimStatus)}
                    disabled={pending || claim.allowedTransitions.length === 0}
                  >
                    <SelectTrigger id="claim-status">
                      <SelectValue placeholder={t('keepStatus')} />
                    </SelectTrigger>
                    <SelectContent>
                      {claim.allowedTransitions.map((next) => (
                        <SelectItem key={next} value={next}>
                          {tStatus(next)}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <label className="flex items-center gap-2 text-sm sm:pb-2.5">
                  <Checkbox
                    checked={isInternal}
                    onCheckedChange={(value) => setIsInternal(value === true)}
                    disabled={pending}
                  />
                  {t('internalNote')}
                </label>
              </div>
            ) : null}

            <Button onClick={send} disabled={pending} className="gap-1.5 self-end">
              {pending ? (
                <Loader2 className="size-4 animate-spin" aria-hidden />
              ) : (
                <Send className="flip-rtl size-4" aria-hidden />
              )}
              {t('send')}
            </Button>
          </CardContent>
        </Card>
      </div>

      {/* Colonne de contexte */}
      <Card className="lg:sticky lg:top-20">
        <CardContent className="flex flex-col gap-4 pt-6 text-sm">
          <Row label={t('number')} value={<span className="font-mono">{claim.code}</span>} />

          {claim.parcelCode ? (
            <Row
              label={t('parcel')}
              value={
                <Link
                  href={`/parcels/${encodeURIComponent(claim.parcelCode)}`}
                  className="text-primary inline-flex items-center gap-1.5 font-mono hover:underline"
                >
                  <Package className="size-3.5" aria-hidden />
                  {claim.parcelCode}
                </Link>
              }
            />
          ) : null}

          <Row
            label={t('dueAt')}
            value={
              claim.dueAt ? (
                <span className="inline-flex items-center gap-1.5">
                  <CalendarClock className="size-3.5" aria-hidden />
                  {format.dateTime(new Date(claim.dueAt), 'short')}
                </span>
              ) : (
                <span className="text-muted-foreground">{t('noDueDate')}</span>
              )
            }
          />

          {claim.resolvedAt ? (
            <Row
              label={t('resolvedAt')}
              value={format.dateTime(new Date(claim.resolvedAt), 'stamp')}
            />
          ) : null}

          {canHandle ? (
            <div className="flex flex-col gap-1.5 border-t pt-4">
              <Label htmlFor="claim-assignee" className="inline-flex items-center gap-1.5">
                <UserCog className="size-3.5" aria-hidden />
                {t('assignee')}
              </Label>
              <SearchableSelect
                options={team.map((member) => ({
                  value: member.id,
                  label: `${member.firstName} ${member.lastName}`,
                }))}
                value={claim.assigneeId ?? UNASSIGNED}
                onValueChange={assign}
                emptyOption={{ value: UNASSIGNED, label: t('unassigned') }}
                disabled={pending}
                id="claim-assignee"
              />
            </div>
          ) : (
            <Row label={t('assignee')} value={claim.assigneeName ?? t('unassigned')} />
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-muted-foreground text-xs tracking-wide uppercase">{label}</span>
      <span>{value}</span>
    </div>
  );
}

/**
 * Les pièces jointes passent par une Server Action, pas par un lien direct :
 * la session vit dans un cookie `httpOnly` posé sur l'origine du web, et un
 * `<a href>` vers l'API partirait sans elle (D-018).
 */
function AttachmentList({
  claim,
  attachments,
}: {
  claim: ClaimDetail;
  attachments: ClaimAttachment[];
}) {
  const t = useTranslations('claims.attachments');
  const [pending, startTransition] = useTransition();

  if (attachments.length === 0) return null;

  const download = (attachment: ClaimAttachment) => {
    startTransition(async () => {
      const result = await claimAttachmentAction(claim.code, attachment.id);
      if (!result.ok) {
        toast.error(result.error);
        return;
      }
      downloadBase64(result.data, attachment.filename, attachment.mimeType);
    });
  };

  return (
    <ul className="flex flex-wrap gap-2">
      {attachments.map((attachment) => (
        <li key={attachment.id}>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={pending}
            onClick={() => download(attachment)}
            className="h-auto gap-1.5 py-1 text-xs"
          >
            <Paperclip className="size-3.5" aria-hidden />
            <span className="max-w-40 truncate">{attachment.filename}</span>
            <span className="text-muted-foreground">
              {t('size', { kb: Math.max(1, Math.round(attachment.sizeBytes / 1024)) })}
            </span>
          </Button>
        </li>
      ))}
    </ul>
  );
}
