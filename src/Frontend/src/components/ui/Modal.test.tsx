import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from './Modal';

const renderModal = (onClose = vi.fn()) => {
  render(
    <Modal open title="Novo lançamento" onClose={onClose}>
      <button type="button">Salvar</button>
    </Modal>,
  );
  return onClose;
};

describe('Modal', () => {
  it('is announced as a dialog with its title', () => {
    renderModal();
    expect(screen.getByRole('dialog', { name: 'Novo lançamento' })).toBeInTheDocument();
  });

  it('renders nothing while closed', () => {
    render(
      <Modal open={false} title="Novo lançamento" onClose={vi.fn()}>
        <span>conteúdo</span>
      </Modal>,
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('closes on Escape', async () => {
    const onClose = renderModal();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('closes on a click outside the panel', async () => {
    const onClose = renderModal();
    await userEvent.click(screen.getByTestId('modal-backdrop'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('does not close on a click inside the panel', async () => {
    // Selecting text in a field and releasing outside used to close the dialog
    // and discard everything typed.
    const onClose = renderModal();
    await userEvent.click(screen.getByRole('button', { name: 'Salvar' }));
    expect(onClose).not.toHaveBeenCalled();
  });
});
