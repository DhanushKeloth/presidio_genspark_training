import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewShipment } from './review-shipment';

describe('ReviewShipment', () => {
  let component: ReviewShipment;
  let fixture: ComponentFixture<ReviewShipment>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewShipment],
    }).compileComponents();

    fixture = TestBed.createComponent(ReviewShipment);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
